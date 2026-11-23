using System.Buffers.Binary;

namespace Pizza.Converters.Docs;

/// <summary>Filtra metadados do PNG incorporado sem decodificar pixels ou quadros de animação.</summary>
internal static class PngMetadata
{
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];
    private const int ChunkOverhead = 12; // Comprimento, tipo e CRC.

    public static byte[] Remove(byte[] image)
    {
        if (!image.AsSpan().StartsWith(Signature))
        {
            throw new InvalidDataException("Assinatura PNG incompleta.");
        }

        var source = Signature.Length;
        var destination = source;
        var hasData = false;
        var hasExif = false;
        while (image.Length - source >= ChunkOverhead)
        {
            var remaining = image.AsSpan(source);
            var length = BinaryPrimitives.ReadUInt32BigEndian(remaining);
            if (length > remaining.Length - ChunkOverhead)
            {
                break;
            }

            var size = (int)length + ChunkOverhead;
            var type = remaining.Slice(4, 4);
            if (source == Signature.Length && (!type.SequenceEqual("IHDR"u8) || length != 13))
            {
                break;
            }

            var isEnd = type.SequenceEqual("IEND"u8);
            hasData |= type.SequenceEqual("IDAT"u8);
            if (type.SequenceEqual("eXIf"u8))
            {
                if (hasExif) throw new InvalidDataException("PNG com mais de um bloco EXIF.");
                hasExif = true;
                var clean = TechnicalExif.Keep(remaining.Slice(8, (int)length));
                if (clean.Length > length) throw new InvalidDataException("Layout dos campos técnicos EXIF inválido.");
                if (clean.Length != 0)
                {
                    var chunk = image.AsSpan(destination, clean.Length + ChunkOverhead);
                    BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)clean.Length);
                    "eXIf"u8.CopyTo(chunk[4..]);
                    clean.CopyTo(chunk[8..]);
                    BinaryPrimitives.WriteUInt32BigEndian(chunk[^4..], Crc32(chunk[4..^4]));
                    destination += chunk.Length;
                }
            }
            else if (!type.SequenceEqual("tEXt"u8) && !type.SequenceEqual("zTXt"u8)
                && !type.SequenceEqual("iTXt"u8) && !type.SequenceEqual("tIME"u8)
                && !type.SequenceEqual("caBX"u8))
            {
                // Copia o chunk completo, com CRC original. A parte pertence ao leitor do DOCX.
                if (destination != source)
                {
                    remaining[..size].CopyTo(image.AsSpan(destination));
                }

                destination += size;
            }

            source += size;
            if (isEnd && length == 0 && hasData)
            {
                return destination == image.Length ? image : image[..destination];
            }

            if (isEnd)
            {
                break;
            }
        }

        throw new InvalidDataException("Estrutura PNG incompleta.");
    }

    // CRC definido pelo PNG para o tipo e o payload. Somente os pequenos chunks EXIF são recriados.
    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }

        return ~crc;
    }
}
