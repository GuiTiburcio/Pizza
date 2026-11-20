using System.Text;

namespace Pizza.Core.Output;

/// <summary>
/// ADR-038: o Worker não sabe o nome final das imagens do Markdown e escreve, no lugar de cada um, U+E000, o
/// número da imagem (1 a 999.999, sem zero à esquerda) e U+E000. O residente troca só esse marcador pelo nome que
/// ele mesmo escolheu e copia o resto igual. Marcador fora do formato recusa o arquivo inteiro.
/// </summary>
public static class MarkdownMarkers
{
    private const int MaxDigits = 6;

    // U+E000 em UTF-8.
    private static readonly byte[] Marker = [0xEE, 0x80, 0x80];

    public static void Replace(Stream input, Stream output, IReadOnlyList<string> names, int bufferBytes = 64 * 1024)
    {
        var reader = new BufferedStream(input, bufferBytes);
        var writer = new BufferedStream(output, bufferBytes);
        int current;
        while ((current = reader.ReadByte()) != -1)
        {
            if (current != Marker[0])
            {
                writer.WriteByte((byte)current);
                continue;
            }

            // O texto já é UTF-8 válido (OutputBundle.ReadFormatted): depois de 0xEE vêm dois bytes de continuação.
            var second = Next(reader);
            var third = Next(reader);
            if (second != Marker[1] || third != Marker[2])
            {
                writer.WriteByte((byte)current);
                writer.WriteByte((byte)second);
                writer.WriteByte((byte)third);
                continue;
            }

            writer.Write(Encoding.UTF8.GetBytes(names[Number(reader, names.Count) - 1]));
        }

        writer.Flush();
    }

    private static int Number(BufferedStream reader, int count)
    {
        var number = 0;
        var digits = 0;
        int current;
        while ((current = Next(reader)) is >= '0' and <= '9')
        {
            if ((digits == 0 && current == '0') || ++digits > MaxDigits)
            {
                throw Invalid();
            }

            number = (number * 10) + (current - '0');
        }

        if (digits == 0 || current != Marker[0] || Next(reader) != Marker[1] || Next(reader) != Marker[2] || number > count)
        {
            throw Invalid();
        }

        return number;
    }

    private static int Next(BufferedStream reader) => reader.ReadByte() is var value and >= 0 ? value : throw Invalid();

    private static InvalidDataException Invalid() => new("Marcador de imagem fora do formato.");
}
