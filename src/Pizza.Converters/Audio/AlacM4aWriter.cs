using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Pizza.Core.Output;

namespace Pizza.Converters.Audio;

/// <summary>Uma trilha ALAC, com duração real e último pacote parcial. Nunca passa pelo sink ALAC do Windows.</summary>
internal sealed unsafe class AlacM4aWriter : IDisposable
{
    private const int FramesPerPacket = 4096;
    private const int MaxMetadataBytes = 64 * 1024 * 1024;
    private readonly Stream _output;
    private readonly AppleAlacCodec.Api _api;
    private readonly byte[] _pcm;
    private readonly byte[] _packet;
    private readonly byte[] _ordered;
    private readonly AlacChannelLayout _layout;
    private readonly int _frameBytes;
    private readonly int _rate, _channels, _bits;
    private readonly List<uint> _sizes = [];
    private readonly long _mediaStart, _dataStart;
    private nint _encoder;
    private int _pending;
    private long _frames;

    internal AlacM4aWriter(Stream output, int rate, int channels, int bits, AlacChannelLayout layout)
    {
        _output = output;
        _api = AppleAlacCodec.Native ?? throw new DllNotFoundException("Encoder ALAC indisponível.");
        _rate = rate; _channels = channels; _bits = bits;
        _layout = layout;
        _frameBytes = checked(channels * (bits / 8));
        uint capacity = 0;
        _encoder = _api.Create((uint)rate, (uint)channels, (uint)bits, &capacity);
        if (_encoder == 0 || capacity is 0 or > 256 * 1024)
        {
            if (_encoder != 0) _api.Destroy(_encoder);
            _encoder = 0;
            throw new InvalidDataException("Parâmetros ALAC recusados.");
        }
        try
        {
            _pcm = new byte[checked(FramesPerPacket * _frameBytes)];
            _packet = new byte[capacity];
            _ordered = channels > 2 ? new byte[_pcm.Length] : [];
            output.Write(Box("ftyp", Join(Text("M4A "), U32(0), Text("M4A isomiso2"))));
            _mediaStart = output.Position;
            output.Write(Join(U32(1), Text("mdat"), U64(16)));
            _dataStart = output.Position;
        }
        catch
        {
            _api.Destroy(_encoder);
            _encoder = 0;
            throw;
        }
    }

    internal void WriteSample(nint sample)
    {
        var buffer = MediaFoundation.ContiguousBuffer(sample);
        try
        {
            var bytes = MediaFoundation.Lock(buffer);
            try
            {
                if (bytes.Length % _frameBytes != 0) throw new InvalidDataException("PCM desalinhado.");
                while (!bytes.IsEmpty)
                {
                    var count = Math.Min(bytes.Length, _pcm.Length - _pending);
                    bytes[..count].CopyTo(_pcm.AsSpan(_pending));
                    _pending += count;
                    bytes = bytes[count..];
                    if (_pending == _pcm.Length) EncodePending();
                }
            }
            finally { MediaFoundation.Unlock(buffer); }
        }
        finally { MediaFoundation.Release(buffer); }
    }

    private void EncodePending()
    {
        if (_pending == 0) return;
        if (_sizes.Count >= MaxMetadataBytes / sizeof(uint) - 1024) throw new ConversionLimitException();
        int size;
        var input = _pcm;
        if (_channels > 2)
        {
            var sampleBytes = _bits / 8;
            for (var frame = 0; frame < _pending; frame += _frameBytes)
            for (var channel = 0; channel < _channels; channel++)
            {
                _pcm.AsSpan(frame + _layout.Order[channel] * sampleBytes, sampleBytes)
                    .CopyTo(_ordered.AsSpan(frame + channel * sampleBytes, sampleBytes));
            }
            input = _ordered;
        }
        fixed (byte* source = input, destination = _packet)
        {
            size = _api.Encode(_encoder, source, _pending, destination, (uint)_packet.Length);
        }
        if (size <= 0 || size > _packet.Length) throw new InvalidDataException("Pacote ALAC inválido.");
        OutputBudget.Check(checked(_output.Position + size));
        _output.Write(_packet.AsSpan(0, size));
        _sizes.Add((uint)size);
        _frames = checked(_frames + _pending / _frameBytes);
        _pending = 0;
    }

    internal bool Complete()
    {
        EncodePending();
        if (_frames == 0) return false;
        if (_frames > uint.MaxValue) throw new ConversionLimitException();
        var config = new byte[24];
        fixed (byte* pointer = config)
        {
            if (_api.Config(_encoder, pointer, (uint)config.Length) != config.Length) throw new InvalidDataException("Configuração ALAC inválida.");
        }
        var end = _output.Position;
        var metadata = Movie(config);
        OutputBudget.Check(checked(end + metadata.Length));
        _output.Write(metadata);
        var final = _output.Position;
        _output.Position = _mediaStart + 8;
        _output.Write(U64(checked((ulong)(end - _mediaStart))));
        _output.Position = final;
        return true;
    }

    public void Dispose()
    {
        if (_encoder != 0) { _api.Destroy(_encoder); _encoder = 0; }
        CryptographicOperations.ZeroMemory(_pcm);
        CryptographicOperations.ZeroMemory(_packet);
        CryptographicOperations.ZeroMemory(_ordered);
    }

    private byte[] Movie(byte[] config)
    {
        var duration = checked((uint)_frames);
        var movie = new byte[100];
        Put(movie, 12, (uint)_rate); Put(movie, 16, duration); Put(movie, 20, 0x10000);
        Put16(movie, 24, 0x100); Matrix(movie, 36); Put(movie, 96, 2);
        var track = new byte[84];
        Put(track, 0, 7); Put(track, 12, 1); Put(track, 20, duration); Put16(track, 36, 0x100); Matrix(track, 40);
        var media = new byte[24];
        Put(media, 12, (uint)_rate); Put(media, 16, duration); Put16(media, 20, 0x55c4);
        var audio = new byte[28];
        Put16(audio, 6, 1); Put16(audio, 16, (ushort)_channels); Put16(audio, 18, (ushort)_bits);
        Put(audio, 24, _rate <= ushort.MaxValue ? (uint)_rate << 16 : 0);
        var channel = _channels <= 2 ? [] : Full("chan", Join(U32(_layout.Tag), U32(0), U32(0)));
        var description = Full("stsd", Join(U32(1), Box("alac", Join(audio, Full("alac", config), channel))));
        var timing = new List<byte[]>();
        var whole = duration / FramesPerPacket;
        var tail = duration % FramesPerPacket;
        if (whole > 0) timing.Add(Join(U32(whole), U32(FramesPerPacket)));
        if (tail > 0) timing.Add(Join(U32(1), U32(tail)));
        var times = Full("stts", Join([U32((uint)timing.Count), .. timing]));
        var sizes = new byte[checked(8 + _sizes.Count * 4)];
        Put(sizes, 4, (uint)_sizes.Count);
        for (var index = 0; index < _sizes.Count; index++) Put(sizes, 8 + index * 4, _sizes[index]);
        var tables = Box("stbl", Join(description, times,
            Full("stsc", Join(U32(1), U32(1), U32((uint)_sizes.Count), U32(1))),
            Full("stsz", sizes), Full("stco", Join(U32(1), U32(checked((uint)_dataStart))))));
        var data = Box("dinf", Full("dref", Join(U32(1), Box("url ", U32(1)))));
        var minf = Box("minf", Join(Full("smhd", new byte[4]), data, tables));
        var handler = Full("hdlr", Join(U32(0), Text("soun"), new byte[12], Text("SoundHandler\0")));
        return Box("moov", Join(Box("mvhd", movie), Box("trak", Join(Box("tkhd", track), Box("mdia", Join(Box("mdhd", media), handler, minf))))));
    }

    private static void Matrix(byte[] data, int offset) { Put(data, offset, 0x10000); Put(data, offset + 16, 0x10000); Put(data, offset + 32, 0x40000000); }
    private static byte[] Text(string value) => Encoding.ASCII.GetBytes(value);
    private static byte[] U32(uint value) { var data = new byte[4]; Put(data, 0, value); return data; }
    private static byte[] U64(ulong value) { var data = new byte[8]; BinaryPrimitives.WriteUInt64BigEndian(data, value); return data; }
    private static void Put(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset, 4), value);
    private static void Put16(byte[] data, int offset, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset, 2), value);
    private static byte[] Box(string name, byte[] data) => Join(U32(checked((uint)data.Length + 8)), Text(name), data);
    private static byte[] Full(string name, byte[] data) => Box(name, Join(new byte[4], data));
    private static byte[] Join(params byte[][] parts)
    {
        var length = parts.Sum(part => (long)part.Length);
        if (length > MaxMetadataBytes) throw new ConversionLimitException();
        var data = new byte[(int)length];
        var offset = 0;
        foreach (var part in parts) { part.CopyTo(data, offset); offset += part.Length; }
        return data;
    }
}
