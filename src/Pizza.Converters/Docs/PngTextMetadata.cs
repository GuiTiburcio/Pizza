using System.Buffers.Binary;

namespace Pizza.Converters.Docs;

/// <summary>Remove texto e data do PNG incorporado sem decodificar pixels ou quadros de animação.</summary>
internal static class PngTextMetadata
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
            if (!type.SequenceEqual("tEXt"u8) && !type.SequenceEqual("zTXt"u8)
                && !type.SequenceEqual("iTXt"u8) && !type.SequenceEqual("tIME"u8))
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
}
