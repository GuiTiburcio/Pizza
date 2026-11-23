using System.Buffers.Binary;

namespace Pizza.Converters.Docs;

/// <summary>Filtra metadados entre os marcadores JPEG e copia os dados comprimidos de cada scan.</summary>
internal static class JpegMetadata
{
    private static ReadOnlySpan<byte> Exif => "Exif\0\0"u8;

    public static byte[] Remove(byte[] image)
    {
        // O buffer da parte é privado. Os segmentos só encolhem, por isso a escrita nunca alcança bytes ainda não lidos.
        using var output = new MemoryStream(image, writable: true);
        output.Position = 2; // SOI, conferido pelo leitor do DOCX.
        var position = 2;
        var scan = false;
        var seenScan = false;
        var seenExif = false;
        while (position < image.Length)
        {
            var start = position;
            if (scan)
            {
                position = EndOfScan(image, position);
                output.Write(image.AsSpan(start, position - start));
            }

            if (position >= image.Length || image[position++] != 0xff) break;
            while (position < image.Length && image[position] == 0xff) position++;
            if (position >= image.Length) break;
            var marker = image[position++];
            if (marker == 0xd9 && seenScan)
            {
                output.Write([0xff, 0xd9]);
                var end = (int)output.Position;
                return end == image.Length ? image : image[..end];
            }

            if (marker == 0x01) // TEM não tem comprimento.
            {
                output.Write([0xff, marker]);
                continue;
            }

            if (marker is 0x00 or >= 0xd0 and <= 0xd9 || image.Length - position < 2) break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(image.AsSpan(position));
            if (length < 2 || length > image.Length - position) break;
            var data = image.AsSpan(position + 2, length - 2);
            switch (marker)
            {
                case 0xe0 when data.StartsWith("JFIF\0"u8) && data.Length >= 14:
                    // Densidade e versão são técnicas. As dimensões do thumbnail ficam zeradas.
                    Header(output, marker, 14);
                    output.Write(data[..12]);
                    output.Write([0, 0]);
                    break;
                case 0xe1 when data.StartsWith(Exif):
                    // Só o primeiro bloco EXIF conta para os leitores; os seguintes saem inteiros.
                    if (seenExif) break;
                    seenExif = true;
                    var retained = TechnicalExif.Keep(data[Exif.Length..]);
                    // A escrita no próprio buffer exige que o bloco novo não cresça; se crescer, o EXIF sai.
                    if (retained.Length != 0 && Exif.Length + retained.Length <= data.Length)
                    {
                        Header(output, marker, Exif.Length + retained.Length);
                        output.Write(Exif);
                        output.Write(retained);
                    }

                    break;
                case 0xe2 when data.StartsWith("ICC_PROFILE\0"u8) && data.Length > 14:
                case 0xee when data.StartsWith("Adobe"u8) && data.Length == 12:
                    Header(output, marker, data.Length);
                    output.Write(data);
                    break;
                case >= 0xe0 and <= 0xef or 0xfe: // APP sem função técnica conhecida e COM.
                    break;
                default:
                    Header(output, marker, data.Length);
                    output.Write(data);
                    break;
            }

            position += length;
            // DNL pode aparecer dentro de um scan, que continua depois do segmento.
            scan = marker == 0xda || (scan && marker == 0xdc);
            seenScan |= scan;
        }

        throw new InvalidDataException("Estrutura JPEG incompleta.");
    }

    private static int EndOfScan(ReadOnlySpan<byte> data, int position)
    {
        while (position < data.Length)
        {
            var next = data[position..].IndexOf((byte)0xff);
            if (next < 0) return data.Length;
            var marker = position + next;
            position = marker + 1;
            while (position < data.Length && data[position] == 0xff) position++;
            if (position == data.Length || data[position] is not (0x00 or >= 0xd0 and <= 0xd7))
            {
                return marker;
            }

            position++;
        }

        return position;
    }

    private static void Header(Stream output, byte marker, int payloadLength)
    {
        Span<byte> header = [0xff, marker, 0, 0];
        BinaryPrimitives.WriteUInt16BigEndian(header[2..], checked((ushort)(2 + payloadLength)));
        output.Write(header);
    }
}
