using System.Buffers.Binary;
using Microsoft.Win32.SafeHandles;

namespace Pizza.Core.Output;

/// <summary>
/// Várias saídas num só temporário (ADR-033, Dividir): "PZB1", a quantidade de partes (int32) e, para cada
/// parte, o tamanho (int64) e os bytes. O Worker escreve; o residente confere o formato e copia cada parte para
/// um arquivo próprio, sem interpretar o conteúdo. Os nomes são sempre do residente (SEC-15, SEC-30).
/// </summary>
public static class OutputBundle
{
    private const int HeaderBytes = 8;
    private const int LengthBytes = 8;
    private static readonly byte[] Magic = "PZB1"u8.ToArray();
    private static readonly byte[] PdfStart = "%PDF-"u8.ToArray();
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public readonly record struct Part(long Offset, long Length);

    /// <summary>
    /// Lê a lista de partes e recusa qualquer coisa fora do formato exato. Cada parte precisa começar como o
    /// formato da extensão: PDF (Dividir) ou CSV em UTF-8 com BOM (Brotinho, ADR-034).
    /// </summary>
    public static IReadOnlyList<Part> Read(SafeFileHandle handle, int maxParts, string extension)
    {
        ReadOnlySpan<byte> expected = extension switch
        {
            "pdf" => PdfStart,
            "csv" => Utf8Bom,
            _ => throw new InvalidDataException("Formato sem pacote de partes."),
        };

        var size = RandomAccess.GetLength(handle);
        Span<byte> header = stackalloc byte[HeaderBytes];
        Require(size >= HeaderBytes && RandomAccess.Read(handle, header, 0) == HeaderBytes);
        Require(header[..4].SequenceEqual(Magic));
        var count = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
        Require(count >= 1 && count <= maxParts);

        var parts = new List<Part>(count);
        Span<byte> length = stackalloc byte[LengthBytes];
        Span<byte> start = stackalloc byte[expected.Length];
        long position = HeaderBytes;
        for (var index = 0; index < count; index++)
        {
            Require(size - position >= LengthBytes && RandomAccess.Read(handle, length, position) == LengthBytes);
            position += LengthBytes;
            var partLength = BinaryPrimitives.ReadInt64LittleEndian(length);
            Require(partLength >= expected.Length && partLength <= size - position);
            Require(RandomAccess.Read(handle, start, position) == expected.Length && start.SequenceEqual(expected));
            parts.Add(new Part(position, partLength));
            position += partLength;
        }

        Require(position == size);
        return parts;
    }

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new InvalidDataException("Pacote de saída fora do formato.");
        }
    }

    /// <summary>Escreve as partes no destino do Worker, na quantidade anunciada.</summary>
    public sealed class Writer
    {
        private readonly Stream _destination;
        private readonly int _count;
        private int _written;

        public Writer(Stream destination, int count)
        {
            _destination = destination;
            _count = count;
            Span<byte> header = stackalloc byte[HeaderBytes];
            Magic.CopyTo(header);
            BinaryPrimitives.WriteInt32LittleEndian(header[4..], count);
            destination.Write(header);
        }

        public void Add(ReadOnlySpan<byte> part)
        {
            if (_written == _count)
            {
                throw new InvalidOperationException("Mais partes que o anunciado.");
            }

            Span<byte> length = stackalloc byte[LengthBytes];
            BinaryPrimitives.WriteInt64LittleEndian(length, part.Length);
            _destination.Write(length);
            _destination.Write(part);
            _written++;
        }

        /// <summary>
        /// Parte escrita em fluxo, sem caber inteira na memória: o que <paramref name="writePart"/> gravar no
        /// destino vira a parte, e o tamanho é preenchido depois. O destino precisa aceitar reposicionamento.
        /// </summary>
        public void Add(Action writePart)
        {
            if (_written == _count)
            {
                throw new InvalidOperationException("Mais partes que o anunciado.");
            }

            var start = _destination.Position;
            Span<byte> length = stackalloc byte[LengthBytes];
            _destination.Write(length);
            writePart();
            var end = _destination.Position;
            BinaryPrimitives.WriteInt64LittleEndian(length, end - start - LengthBytes);
            _destination.Position = start;
            _destination.Write(length);
            _destination.Position = end;
            _written++;
        }

        public void Finish()
        {
            if (_written != _count)
            {
                throw new InvalidOperationException("Menos partes que o anunciado.");
            }
        }
    }
}
