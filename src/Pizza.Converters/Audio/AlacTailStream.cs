using System.Buffers.Binary;
using System.Text;
using Pizza.Core.Output;

namespace Pizza.Converters.Audio;

/// <summary>
/// Preserva o último quadro parcial do ALAC. O MF do Windows completa o quadro com zeros; o quadro de escape do
/// formato permite gravar exatamente o PCM restante. Os quadros completos continuam comprimidos pelo MF.
/// Só observa as próprias escritas: o handle de destino continua sem permissão de leitura (SEC-22).
/// </summary>
internal sealed class AlacTailStream(Stream inner, int channels, int bits, int rate) : Stream
{
    private const int FrameLength = 4096;
    private const int MetadataLimit = 64 * 1024 * 1024;
    private readonly byte[] _prefix = new byte[4096];
    private readonly byte[] _tail = new byte[FrameLength * channels * (bits / 8)];
    private readonly List<(long Offset, byte[] Bytes)> _finalWrites = [];
    private long _pcmBytes;
    private int _captured;
    private bool _finalizing;
    private bool _exceeded;

    public override bool CanRead => false;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }
    public override void Flush() => inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        var position = inner.Position;
        if (position < _prefix.Length)
        {
            buffer[..Math.Min(buffer.Length, _prefix.Length - checked((int)position))].CopyTo(_prefix.AsSpan((int)position));
        }

        if (_finalizing)
        {
            if (buffer.Length > MetadataLimit - _captured)
            {
                _exceeded = true;
                throw new ConversionLimitException();
            }

            _finalWrites.Add((position, buffer.ToArray()));
            _captured += buffer.Length;
        }

        inner.Write(buffer);
    }

    public void Record(nint sample)
    {
        var buffer = MediaFoundation.ContiguousBuffer(sample);
        try
        {
            var pcm = MediaFoundation.Lock(buffer);
            try
            {
                Require(pcm.Length % (channels * (bits / 8)) == 0);
                while (!pcm.IsEmpty)
                {
                    var offset = (int)(_pcmBytes % _tail.Length);
                    var count = Math.Min(pcm.Length, _tail.Length - offset);
                    pcm[..count].CopyTo(_tail.AsSpan(offset));
                    _pcmBytes = checked(_pcmBytes + count);
                    pcm = pcm[count..];
                }
            }
            finally
            {
                MediaFoundation.Unlock(buffer);
            }
        }
        finally
        {
            MediaFoundation.Release(buffer);
        }
    }

    public void BeginFinalization() => _finalizing = _pcmBytes % _tail.Length != 0;

    public void CheckLimit()
    {
        if (_exceeded) throw new ConversionLimitException();
    }

    public void Complete()
    {
        CheckLimit();
        if (!_finalizing) return;
        _finalizing = false;
        var frames = _pcmBytes / (channels * (bits / 8));
        var partialFrames = checked((int)(frames % FrameLength));
        var packet = PartialPacket(_tail.AsSpan(0, partialFrames * channels * (bits / 8)), channels, bits);
        var media = FindMedia();
        var moovHeader = Captured(media.End, 8);
        Require(Encoding.ASCII.GetString(moovHeader, 4, 4) == "moov");
        var moovLength = checked((int)U32(moovHeader, 0));
        Require(moovLength >= 8 && moovLength <= MetadataLimit && media.End + moovLength == inner.Length);
        var moov = Atom.Parse(Captured(media.End, moovLength)).Single();
        Require(moov.Children!.Count(atom => atom.Name == "trak") == 1);
        var sizes = moov.Find("stsz");
        var packetCount = checked((int)U32(sizes.Data, 8));
        Require(packetCount == (frames + FrameLength - 1) / FrameLength);
        var fixedSize = U32(sizes.Data, 4);
        Require(fixedSize != 0 || sizes.Data.Length == 12L + packetCount * 4L);
        uint Size(int index) => fixedSize == 0 ? U32(sizes.Data, 12 + index * 4) : fixedSize;
        var offsets = moov.FindEither("stco", "co64");
        var chunks = checked((int)U32(offsets.Data, 4));
        var offsetWidth = offsets.Name == "stco" ? 4 : 8;
        Require(chunks > 0 && offsets.Data.Length == 8L + chunks * (long)offsetWidth);
        var lastChunkOffset = offsets.Name == "stco" ? U32(offsets.Data, 8 + (chunks - 1) * 4)
            : checked((long)BinaryPrimitives.ReadUInt64BigEndian(offsets.Data.AsSpan(8 + (chunks - 1) * 8, 8)));
        var mapping = moov.Find("stsc").Data;
        var entries = checked((int)U32(mapping, 4));
        Require(entries > 0 && mapping.Length == 8L + entries * 12L);
        var lastChunkSamples = 0;
        for (var entry = 0; entry < entries; entry++)
        {
            if (U32(mapping, 8 + entry * 12) <= chunks) lastChunkSamples = checked((int)U32(mapping, 12 + entry * 12));
        }

        Require(lastChunkSamples > 0 && lastChunkSamples <= packetCount);
        var packetOffset = lastChunkOffset;
        for (var index = packetCount - lastChunkSamples; index < packetCount - 1; index++) packetOffset = checked(packetOffset + Size(index));
        var oldSize = Size(packetCount - 1);
        Require(packetOffset >= media.Payload && packetOffset + oldSize == media.End);

        var newSizes = new byte[checked(12 + packetCount * 4)];
        sizes.Data.AsSpan(0, 4).CopyTo(newSizes);
        Put32(newSizes, 8, (uint)packetCount);
        for (var index = 0; index < packetCount; index++) Put32(newSizes, 12 + index * 4, index == packetCount - 1 ? (uint)packet.Length : Size(index));
        sizes.Data = newSizes;
        SetDurations(moov, frames, partialFrames, packetCount, rate);
        UpdateCookie(moov.Find("stsd").Data, packet.Length);
        var metadata = moov.Encode();
        var newEnd = checked(packetOffset + packet.Length + metadata.Length);
        OutputBudget.Check(newEnd);
        inner.Position = packetOffset;
        inner.Write(packet);
        inner.Write(metadata);
        inner.SetLength(newEnd);
        inner.Position = media.Position;
        var header = _prefix.AsSpan(media.Position, media.Header).ToArray();
        var mediaLength = checked(packetOffset + packet.Length - media.Position);
        if (media.Header == 16) BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(8, 8), (ulong)mediaLength);
        else Put32(header, 0, checked((uint)mediaLength));
        inner.Write(header);
        inner.Position = newEnd;
        _finalWrites.Clear();
    }

    private (int Position, int Payload, long End, int Header) FindMedia()
    {
        for (var position = 0; position <= _prefix.Length - 16;)
        {
            var size = U32(_prefix, position);
            var header = size == 1 ? 16 : 8;
            var length = size == 1 ? checked((long)BinaryPrimitives.ReadUInt64BigEndian(_prefix.AsSpan(position + 8, 8))) : size;
            Require(length >= header && length <= inner.Length - position);
            if (Encoding.ASCII.GetString(_prefix, position + 4, 4) == "mdat") return (position, position + header, position + length, header);
            position = checked(position + (int)length);
        }

        throw new InvalidDataException("Cabeçalho M4A inesperado.");
    }

    private byte[] Captured(long offset, int count)
    {
        Require(count >= 0 && count <= MetadataLimit);
        var result = new byte[count];
        var covered = new byte[count];
        foreach (var write in _finalWrites)
        {
            var begin = Math.Max(offset, write.Offset);
            var end = Math.Min(offset + count, write.Offset + write.Bytes.Length);
            if (end <= begin) continue;
            var target = checked((int)(begin - offset));
            var length = checked((int)(end - begin));
            write.Bytes.AsSpan(checked((int)(begin - write.Offset)), length).CopyTo(result.AsSpan(target));
            covered.AsSpan(target, length).Fill(1);
        }

        Require(!covered.Contains((byte)0));
        return result;
    }

    private static void SetDurations(Atom moov, long frames, int partial, int packets, int sampleRate)
    {
        foreach (var name in new[] { "mvhd", "mdhd" })
        {
            var data = moov.Find(name).Data;
            Require(data[0] is 0 or 1);
            var scale = data[0] == 0 ? 12 : 20;
            Put32(data, scale, (uint)sampleRate);
            PutDuration(data, scale + 4, frames);
        }

        var track = moov.Find("tkhd").Data;
        PutDuration(track, track[0] == 0 ? 20 : 28, frames);
        var table = new byte[packets == 1 ? 16 : 24];
        Put32(table, 4, packets == 1 ? 1u : 2u);
        if (packets > 1)
        {
            Put32(table, 8, (uint)(packets - 1));
            Put32(table, 12, FrameLength);
        }

        Put32(table, table.Length - 8, 1);
        Put32(table, table.Length - 4, (uint)partial);
        moov.Find("stts").Data = table;
    }

    private static void PutDuration(byte[] data, int offset, long frames)
    {
        Require(data[0] is 0 or 1);
        if (data[0] == 0) Put32(data, offset, checked((uint)frames));
        else BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(offset, 8), checked((ulong)frames));
    }

    private static void UpdateCookie(byte[] stsd, int packetBytes)
    {
        Require(U32(stsd, 4) == 1 && Encoding.ASCII.GetString(stsd, 12, 4) == "alac");
        // AudioSampleEntry v0: cabeçalho de 36 bytes; depois vêm os átomos específicos do codec.
        for (var position = 44; position <= stsd.Length - 8;)
        {
            var length = checked((int)U32(stsd, position));
            Require(length >= 8 && length <= stsd.Length - position);
            if (Encoding.ASCII.GetString(stsd, position + 4, 4) == "alac")
            {
                Require(length >= 36);
                Put32(stsd, position + 24, Math.Max(U32(stsd, position + 24), (uint)packetBytes));
                return;
            }

            position += length;
        }

        throw new InvalidDataException("Configuração ALAC ausente.");
    }

    internal static byte[] PartialPacket(ReadOnlySpan<byte> pcm, int channelCount, int depth)
    {
        // Estrutura do formato, sem biblioteca adicional: quadro de escape com comprimento explícito.
        // https://github.com/macosforge/alac/blob/master/codec/ALACEncoder.cpp
        Require(channelCount is >= 1 and <= 8 && depth is 16 or 24);
        var sampleBytes = depth / 8;
        Require(pcm.Length % (channelCount * sampleBytes) == 0);
        var frameCount = pcm.Length / (channelCount * sampleBytes);
        Require(frameCount is > 0 and < FrameLength);
        var elements = channelCount == 2 ? 1 : channelCount;
        var result = new byte[checked((pcm.Length * 8 + elements * 55 + 3 + 7) / 8)];
        var bit = 0;
        for (var element = 0; element < elements; element++)
        {
            var pair = channelCount == 2;
            Write(pair ? 1u : 0u, 3);
            Write((uint)element, 4);
            Write(0, 12);
            Write(9, 4);
            Write((uint)frameCount, 32);
            for (var frame = 0; frame < frameCount; frame++)
            for (var channel = pair ? 0 : element; channel < (pair ? 2 : element + 1); channel++)
            {
                var offset = (frame * channelCount + channel) * sampleBytes;
                uint value = 0;
                for (var index = sampleBytes - 1; index >= 0; index--) value = (value << 8) | pcm[offset + index];
                Write(value, depth);
            }
        }

        Write(7, 3);
        return result;

        void Write(uint value, int count)
        {
            for (var shift = count - 1; shift >= 0; shift--, bit++) result[bit / 8] |= (byte)(((value >> shift) & 1) << (7 - bit % 8));
        }
    }

    private static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
    private static void Put32(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset, 4), value);
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException("Estrutura ALAC inesperada.");
    }

    private sealed class Atom(string name, byte[] data, List<Atom>? children)
    {
        public string Name { get; } = name;
        public byte[] Data { get; set; } = data;
        public List<Atom>? Children { get; } = children;
        public Atom Find(string name) => Descendants().Single(atom => atom.Name == name);
        public Atom FindEither(string first, string second) => Descendants().Single(atom => atom.Name == first || atom.Name == second);
        private IEnumerable<Atom> Descendants() => Children is null ? [] : Children.SelectMany(child => new[] { child }.Concat(child.Descendants()));
        public byte[] Encode()
        {
            var payload = Children is null ? Data : Children.SelectMany(child => child.Encode()).ToArray();
            var result = new byte[checked(8 + payload.Length)];
            Put32(result, 0, (uint)result.Length);
            Encoding.ASCII.GetBytes(Name, result.AsSpan(4, 4));
            payload.CopyTo(result, 8);
            return result;
        }

        public static List<Atom> Parse(byte[] bytes)
        {
            var result = new List<Atom>();
            for (var position = 0; position < bytes.Length;)
            {
                Require(bytes.Length - position >= 8);
                var length = checked((int)U32(bytes, position));
                Require(length >= 8 && length <= bytes.Length - position);
                var name = Encoding.ASCII.GetString(bytes, position + 4, 4);
                var payload = bytes.AsSpan(position + 8, length - 8).ToArray();
                result.Add(new Atom(name, payload, name is "moov" or "trak" or "mdia" or "minf" or "stbl" ? Parse(payload) : null));
                position += length;
            }

            return result;
        }
    }
}
