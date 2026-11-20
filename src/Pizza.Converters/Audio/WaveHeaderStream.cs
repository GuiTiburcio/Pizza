using System.Buffers.Binary;

namespace Pizza.Converters.Audio;

/// <summary>O alinhamento RIFF fica fora do tamanho válido do chunk data, inclusive em PCM mono de 24 bits.</summary>
internal sealed class WaveHeaderStream(Stream inner) : Stream
{
    private readonly byte[] _header = new byte[4096];
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
        if (position < _header.Length)
        {
            buffer[..Math.Min(buffer.Length, _header.Length - checked((int)position))].CopyTo(_header.AsSpan((int)position));
        }

        inner.Write(buffer);
    }

    public void Complete(long pcmBytes)
    {
        // SEC-22: cabeçalho observado nas próprias escritas, sem ler ou reabrir o destino.
        if (!_header.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !_header.AsSpan(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Cabeçalho WAV inesperado.");
        for (var position = 12; position <= _header.Length - 8;)
        {
            var size = BinaryPrimitives.ReadUInt32LittleEndian(_header.AsSpan(position + 4, 4));
            if (_header.AsSpan(position, 4).SequenceEqual("data"u8))
            {
                if (size != pcmBytes && size != pcmBytes + (pcmBytes & 1))
                    throw new InvalidDataException("Comprimento WAV inesperado.");
                if (size != pcmBytes)
                {
                    inner.Position = position + 4;
                    var valid = _header.AsSpan(position + 4, 4);
                    BinaryPrimitives.WriteUInt32LittleEndian(valid, checked((uint)pcmBytes));
                    inner.Write(valid);
                    inner.Position = inner.Length;
                }

                return;
            }

            position = checked(position + 8 + (int)size + ((int)size & 1));
        }

        throw new InvalidDataException("Chunk WAV ausente.");
    }
}
