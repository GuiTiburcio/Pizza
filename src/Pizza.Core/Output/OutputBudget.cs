namespace Pizza.Core.Output;

/// <summary>SEC-40: teto agregado dos arquivos finais, sem reduzir a qualidade para caber.</summary>
public static class OutputBudget
{
    public const long MaxBytes = 2L * 1024 * 1024 * 1024;

    // O envelope não faz parte dos arquivos finais; cada parte acrescenta um int64 ao cabeçalho.
    public static long ContainerBytes(bool bundled) =>
        MaxBytes + (bundled ? 8L + (8L * Ipc.ConversionResult.MaxParts) : 0);

    public static void Check(long bytes, long limit = MaxBytes)
    {
        if (bytes > limit)
        {
            throw new ConversionLimitException();
        }
    }
}

/// <summary>Um limite de conversão foi excedido; não é ausência de metadados nem arquivo corrompido.</summary>
public sealed class ConversionLimitException : Exception
{
}

/// <summary>
/// Limita o comprimento de saída, mantendo seek e reescrita de cabeçalhos. O dono continua responsável pelo
/// stream interno. Exceeded permanece ligado mesmo quando um codec nativo transforma a exceção em HRESULT.
/// </summary>
public sealed class OutputBudgetStream(Stream inner, long limit = OutputBudget.MaxBytes) : Stream
{
    public bool Exceeded { get; private set; }

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position
    {
        get => inner.Position;
        set
        {
            Check(value);
            inner.Position = value;
        }
    }

    public override void Flush() => inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => inner.Read(buffer);

    public override long Seek(long offset, SeekOrigin origin)
    {
        var position = inner.Seek(offset, origin);
        Check(position);
        return position;
    }

    public override void SetLength(long value)
    {
        Check(value);
        inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length > limit - inner.Position)
        {
            Exceeded = true;
        }

        Check(inner.Position);
        inner.Write(buffer);
    }

    private void Check(long length)
    {
        Exceeded |= length > limit;
        if (Exceeded)
        {
            throw new ConversionLimitException();
        }
    }
}
