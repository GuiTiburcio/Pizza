using System.Buffers.Binary;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Pizza.Core.Output;

/// <summary>Formato de uma parte do pacote com formatos (ADR-038). O número vai no arquivo; não mudar.</summary>
public enum PartFormat : byte
{
    Markdown = 1,
    Png = 2,
    Jpeg = 3,
    Gif = 4,
    Webp = 5,
}

/// <summary>
/// Várias saídas num só temporário. O Worker escreve; o residente confere o formato e copia cada parte para um
/// arquivo próprio. Os nomes são sempre do residente (SEC-15, SEC-30).
/// <list type="bullet">
/// <item>"PZB1" (ADR-033, Dividir): a quantidade de partes (int32) e, para cada parte, o tamanho (int64) e os
/// bytes, todas do formato da extensão.</item>
/// <item>"PZB2" (ADR-038, Markdown): igual, mas cada parte começa com um byte de formato, de uma lista fixa
/// que o residente tira da extensão da conversão (SEC-13), nunca do Worker.</item>
/// </list>
/// </summary>
public static class OutputBundle
{
    private const int HeaderBytes = 8;
    private const int LengthBytes = 8;
    private static readonly byte[] Magic = "PZB1"u8.ToArray();
    private static readonly byte[] FormattedMagic = "PZB2"u8.ToArray();
    private static readonly byte[] PdfStart = "%PDF-"u8.ToArray();
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public readonly record struct Part(long Offset, long Length);

    public readonly record struct FormattedPart(long Offset, long Length, PartFormat Format);

    /// <summary>
    /// Lê a lista de partes e recusa qualquer coisa fora do formato exato. Cada parte precisa começar como o
    /// formato da extensão: PDF (Dividir) ou CSV em UTF-8 com BOM (Brotinho, ADR-034).
    /// </summary>
    public static IReadOnlyList<Part> Read(SafeFileHandle handle, int maxParts, string extension, long maxOutputBytes = OutputBudget.MaxBytes)
    {
        ReadOnlySpan<byte> expected = extension switch
        {
            "pdf" => PdfStart,
            "csv" => Utf8Bom,
            _ => throw new InvalidDataException("Formato sem pacote de partes."),
        };

        var size = RandomAccess.GetLength(handle);
        OutputBudget.Check(size, maxOutputBytes + HeaderBytes + ((long)LengthBytes * maxParts));
        var count = Header(handle, size, Magic, maxParts);
        var parts = new List<Part>(count);
        Span<byte> length = stackalloc byte[LengthBytes];
        Span<byte> start = stackalloc byte[expected.Length];
        long position = HeaderBytes;
        long total = 0;
        for (var index = 0; index < count; index++)
        {
            Require(size - position >= LengthBytes && RandomAccess.Read(handle, length, position) == LengthBytes);
            position += LengthBytes;
            var partLength = BinaryPrimitives.ReadInt64LittleEndian(length);
            Require(partLength >= expected.Length && partLength <= size - position);
            OutputBudget.Check(partLength, maxOutputBytes - total);
            total += partLength;
            Require(RandomAccess.Read(handle, start, position) == expected.Length && start.SequenceEqual(expected));
            parts.Add(new Part(position, partLength));
            position += partLength;
        }

        Require(position == size);
        return parts;
    }

    /// <summary>
    /// Lê um pacote com formatos (ADR-038). Para <c>md</c>: exatamente um Markdown em UTF-8 válido, o primeiro,
    /// e depois só imagens que comecem pela assinatura do seu formato. Fora disso, nada é aceito. O total passa
    /// pelo mesmo orçamento de saída do PZB1.
    /// </summary>
    public static IReadOnlyList<FormattedPart> ReadFormatted(SafeFileHandle handle, int maxParts, string extension, long maxOutputBytes = OutputBudget.MaxBytes)
    {
        Require(extension == "md");
        var size = RandomAccess.GetLength(handle);
        OutputBudget.Check(size, maxOutputBytes + HeaderBytes + ((long)(1 + LengthBytes) * maxParts));
        var count = Header(handle, size, FormattedMagic, maxParts);
        var parts = new List<FormattedPart>(count);
        Span<byte> head = stackalloc byte[1 + LengthBytes];
        long position = HeaderBytes;
        long total = 0;
        for (var index = 0; index < count; index++)
        {
            Require(size - position >= head.Length && RandomAccess.Read(handle, head, position) == head.Length);
            position += head.Length;
            var format = (PartFormat)head[0];
            var partLength = BinaryPrimitives.ReadInt64LittleEndian(head[1..]);
            Require(partLength >= 0 && partLength <= size - position);
            OutputBudget.Check(partLength, maxOutputBytes - total);
            total += partLength;
            Require(index == 0 ? format == PartFormat.Markdown : format is PartFormat.Png or PartFormat.Jpeg or PartFormat.Gif or PartFormat.Webp);
            Require(format == PartFormat.Markdown ? IsUtf8(handle, position, partLength) : HasSignature(handle, position, partLength, format));
            parts.Add(new FormattedPart(position, partLength, format));
            position += partLength;
        }

        Require(position == size);
        return parts;
    }

    public static string Extension(PartFormat format) => format switch
    {
        PartFormat.Markdown => "md",
        PartFormat.Png => "png",
        PartFormat.Jpeg => "jpg",
        PartFormat.Gif => "gif",
        PartFormat.Webp => "webp",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    private static int Header(SafeFileHandle handle, long size, byte[] magic, int maxParts)
    {
        Span<byte> header = stackalloc byte[HeaderBytes];
        Require(size >= HeaderBytes && RandomAccess.Read(handle, header, 0) == HeaderBytes);
        Require(header[..4].SequenceEqual(magic));
        var count = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
        Require(count >= 1 && count <= maxParts);
        return count;
    }

    private static bool HasSignature(SafeFileHandle handle, long position, long length, PartFormat format)
    {
        Span<byte> start = stackalloc byte[12];
        var read = RandomAccess.Read(handle, start[..(int)Math.Min(start.Length, length)], position);
        return format switch
        {
            PartFormat.Png => start[..read] is [0x89, (byte)'P', (byte)'N', (byte)'G', ..],
            PartFormat.Jpeg => start[..read] is [0xFF, 0xD8, 0xFF, ..],
            PartFormat.Gif => start[..read] is [(byte)'G', (byte)'I', (byte)'F', (byte)'8', ..],
            PartFormat.Webp => start[..read] is [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P'],
            _ => false,
        };
    }

    /// <summary>UTF-8 válido do começo ao fim, lido em pedaços (o decodificador guarda o que fica entre eles).</summary>
    private static bool IsUtf8(SafeFileHandle handle, long position, long length)
    {
        var decoder = new UTF8Encoding(false, throwOnInvalidBytes: true).GetDecoder();
        var bytes = new byte[64 * 1024];
        var chars = new char[bytes.Length + 1];
        try
        {
            for (long done = 0; done < length;)
            {
                var read = RandomAccess.Read(handle, bytes.AsSpan(0, (int)Math.Min(bytes.Length, length - done)), position + done);
                if (read <= 0)
                {
                    return false;
                }

                done += read;
                decoder.GetChars(bytes, 0, read, chars, 0, flush: done == length);
            }

            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new InvalidDataException("Pacote de saída fora do formato.");
        }
    }

    /// <summary>Cópia residente por blocos, com cancelamento e prazo conferidos pelo chamador.</summary>
    internal static void CopyPart(SafeFileHandle source, Part part, SafeFileHandle destination, byte[] buffer, Action check)
    {
        for (long copied = 0; copied < part.Length;)
        {
            check();
            var read = RandomAccess.Read(source, buffer.AsSpan(0, (int)Math.Min(buffer.Length, part.Length - copied)), part.Offset + copied);
            if (read <= 0)
            {
                throw new InvalidDataException("Pacote de saída cortado.");
            }

            RandomAccess.Write(destination, buffer.AsSpan(0, read), copied);
            copied += read;
        }

        check();
    }

    /// <summary>Escreve as partes no destino do Worker, na quantidade anunciada; com formatos, no PZB2.</summary>
    public sealed class Writer
    {
        private readonly Stream _destination;
        private readonly int _count;
        private readonly bool _formatted;
        private int _written;
        private long _payload;

        public Writer(Stream destination, int count, bool formatted = false)
        {
            _destination = destination;
            _count = count;
            _formatted = formatted;
            Span<byte> header = stackalloc byte[HeaderBytes];
            (formatted ? FormattedMagic : Magic).CopyTo(header);
            BinaryPrimitives.WriteInt32LittleEndian(header[4..], count);
            destination.Write(header);
        }

        public void Add(ReadOnlySpan<byte> part)
        {
            Start(formatted: false);
            Whole(part);
        }

        public void Add(PartFormat format, ReadOnlySpan<byte> part)
        {
            Start(formatted: true);
            _destination.WriteByte((byte)format);
            Whole(part);
        }

        /// <summary>
        /// Parte escrita em fluxo, sem caber inteira na memória: o que <paramref name="writePart"/> gravar no
        /// destino vira a parte, e o tamanho é preenchido depois. O destino precisa aceitar reposicionamento.
        /// </summary>
        public void Add(Action writePart)
        {
            Start(formatted: false);
            Streamed(writePart);
        }

        public void Add(PartFormat format, Action writePart)
        {
            Start(formatted: true);
            _destination.WriteByte((byte)format);
            Streamed(writePart);
        }

        public void Finish()
        {
            if (_written != _count)
            {
                throw new InvalidOperationException("Menos partes que o anunciado.");
            }
        }

        private void Start(bool formatted)
        {
            if (formatted != _formatted)
            {
                throw new InvalidOperationException(formatted ? "Pacote sem formatos." : "Pacote com formatos.");
            }

            if (_written == _count)
            {
                throw new InvalidOperationException("Mais partes que o anunciado.");
            }
        }

        private void Whole(ReadOnlySpan<byte> part)
        {
            OutputBudget.Check(part.Length, OutputBudget.MaxBytes - _payload);
            Span<byte> length = stackalloc byte[LengthBytes];
            BinaryPrimitives.WriteInt64LittleEndian(length, part.Length);
            _destination.Write(length);
            _destination.Write(part);
            _payload += part.Length;
            _written++;
        }

        private void Streamed(Action writePart)
        {
            var start = _destination.Position;
            Span<byte> length = stackalloc byte[LengthBytes];
            _destination.Write(length);
            writePart();
            var end = _destination.Position;
            OutputBudget.Check(end - start - LengthBytes, OutputBudget.MaxBytes - _payload);
            _payload += end - start - LengthBytes;
            BinaryPrimitives.WriteInt64LittleEndian(length, end - start - LengthBytes);
            _destination.Position = start;
            _destination.Write(length);
            _destination.Position = end;
            _written++;
        }
    }
}
