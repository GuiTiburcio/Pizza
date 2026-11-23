using System.Buffers.Binary;

namespace Pizza.Converters.Docs;

/// <summary>Recria o IFD principal com orientação e densidade, sem seguir ponteiros para outros diretórios.</summary>
internal static class TechnicalExif
{
    private const int EntrySize = 12;
    private static ReadOnlySpan<ushort> TechnicalTags => [0x0112, 0x011a, 0x011b, 0x0128];

    /// <summary>
    /// Nunca falha: um EXIF fora do padrão só perde o que não dá para ler com segurança.
    /// Estrutura inválida descarta o bloco inteiro; campo técnico inválido ou repetido descarta só o campo.
    /// Descartar nunca vaza dado (PRIV-30); o custo é a imagem perder a orientação ou a densidade.
    /// </summary>
    public static byte[] Keep(ReadOnlySpan<byte> tiff)
    {
        var little = tiff.StartsWith("II"u8);
        if (tiff.Length < 8 || (!little && !tiff.StartsWith("MM"u8)) || Read16(tiff[2..], little) != 42)
        {
            return [];
        }

        var offset = Read32(tiff[4..], little);
        if (offset < 8 || offset > tiff.Length - 6)
        {
            return [];
        }

        var directory = tiff[(int)offset..];
        var entries = Read16(directory, little);
        if (entries > (directory.Length - 6) / EntrySize)
        {
            return [];
        }

        Span<Field> fields = stackalloc Field[4];
        fields.Clear();
        for (var index = 0; index < entries; index++)
        {
            var entry = directory.Slice(2 + index * EntrySize, EntrySize);
            var tag = TechnicalTags.IndexOf(Read16(entry, little));
            if (tag < 0)
            {
                continue;
            }

            var type = Read16(entry[2..], little);
            var rational = tag is 1 or 2;
            if (fields[tag].Type != 0 || Read32(entry[4..], little) != 1 || type != (rational ? 5 : 3))
            {
                continue;
            }

            if (rational)
            {
                var position = Read32(entry[8..], little);
                if (position < 8 || position > tiff.Length - 8)
                {
                    continue;
                }

                var value = tiff[(int)position..];
                var denominator = Read32(value[4..], little);
                if (denominator != 0)
                {
                    fields[tag] = new(type, Read32(value, little), denominator);
                }
            }
            else
            {
                var value = Read16(entry[8..], little);
                if (value >= 1 && value <= (tag == 0 ? 8 : 3))
                {
                    fields[tag] = new(type, value, 0);
                }
            }
        }

        return Write(fields);
    }

    private static byte[] Write(ReadOnlySpan<Field> fields)
    {
        ushort count = 0;
        var rationalBytes = 0;
        // É válido X/YResolution apontarem para o mesmo RATIONAL. Mantenha a possibilidade de compartilhamento.
        var sharedResolution = fields[1].Type == 5 && fields[1] == fields[2];
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            if (field.Type != 0) count++;
            if (field.Type == 5 && !(index == 2 && sharedResolution)) rationalBytes += 8;
        }

        if (count == 0) return [];
        var values = 14 + count * EntrySize;
        var result = new byte[values + rationalBytes];
        "II"u8.CopyTo(result);
        Put16(result.AsSpan(2), 42);
        Put32(result.AsSpan(4), 8);
        Put16(result.AsSpan(8), count);
        var position = 10;
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            if (field.Type == 0) continue;
            var entry = result.AsSpan(position, EntrySize);
            Put16(entry, TechnicalTags[index]);
            Put16(entry[2..], field.Type);
            Put32(entry[4..], 1);
            if (field.Type == 5)
            {
                if (index == 2 && sharedResolution)
                {
                    Put32(entry[8..], (uint)(values - 8));
                }
                else
                {
                    Put32(entry[8..], (uint)values);
                    Put32(result.AsSpan(values), field.Value);
                    Put32(result.AsSpan(values + 4), field.Denominator);
                    values += 8;
                }
            }
            else
            {
                Put16(entry[8..], (ushort)field.Value);
            }

            position += EntrySize;
        }

        return result;
    }

    private readonly record struct Field(ushort Type, uint Value, uint Denominator);
    private static ushort Read16(ReadOnlySpan<byte> value, bool little) =>
        little ? BinaryPrimitives.ReadUInt16LittleEndian(value) : BinaryPrimitives.ReadUInt16BigEndian(value);
    private static uint Read32(ReadOnlySpan<byte> value, bool little) =>
        little ? BinaryPrimitives.ReadUInt32LittleEndian(value) : BinaryPrimitives.ReadUInt32BigEndian(value);
    private static void Put16(Span<byte> target, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(target, value);
    private static void Put32(Span<byte> target, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(target, value);
}
