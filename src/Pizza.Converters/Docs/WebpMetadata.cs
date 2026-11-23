using System.Buffers.Binary;

namespace Pizza.Converters.Docs;

/// <summary>Recria o contêiner RIFF conservando os pixels WebP, perfil ICC e quadros comprimidos.</summary>
internal static class WebpMetadata
{
    private const byte ExifFlag = 0x08;
    private const byte XmpFlag = 0x04;

    public static byte[] Remove(byte[] image)
    {
        var size = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(4));
        if (size < 4 || size > image.Length - 8) throw Invalid();
        // Compacta a parte no próprio buffer. O tamanho de cada chunk novo é menor ou igual ao original.
        using var output = new MemoryStream(image, 0, image.Length, writable: true, publiclyVisible: true);
        output.Position = 12;
        ReadOnlySpan<byte> source = image.AsSpan(12, (int)size - 4);
        var seenExif = false;
        var retainedExif = false;
        var extended = false;
        var hasImage = false;
        while (!source.IsEmpty)
        {
            var first = output.Position == 12;
            var data = ReadChunk(ref source, out var type);
            if (type.SequenceEqual("VP8X"u8))
            {
                if (!first || extended || data.Length != 10) throw Invalid();
                extended = true;
                WriteChunk(output, type, data);
            }
            else if (type.SequenceEqual("EXIF"u8))
            {
                // Só o primeiro EXIF de um WebP estendido conta; os demais, e um bloco que cresceria, saem inteiros.
                var prefix = data.StartsWith("Exif\0\0"u8) ? 6 : 0;
                var technical = !extended || seenExif ? [] : TechnicalExif.Keep(data[prefix..]);
                seenExif = true;
                if (technical.Length != 0 && prefix + technical.Length <= data.Length)
                {
                    if (prefix == 0) WriteChunk(output, type, technical);
                    else WriteChunk(output, type, [.. "Exif\0\0"u8, .. technical]);
                    retainedExif = true;
                }
            }
            else if (type.SequenceEqual("ANMF"u8))
            {
                if (data.Length < 16 || !extended) throw Invalid();
                WriteFrame(output, data);
                hasImage = true;
            }
            else if (IsPixels(type) || type.SequenceEqual("ICCP"u8) || type.SequenceEqual("ANIM"u8)
                     || type.SequenceEqual("ALPH"u8))
            {
                hasImage |= IsPixels(type);
                WriteChunk(output, type, data);
            }
        }

        if (!hasImage) throw Invalid();
        var end = (int)output.Position;
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(4), (uint)(end - 8));
        if (extended)
        {
            image[20] &= unchecked((byte)~(ExifFlag | XmpFlag));
            if (retainedExif) image[20] |= ExifFlag;
        }

        return end == image.Length ? image : image[..end];
    }

    private static void WriteFrame(MemoryStream output, ReadOnlySpan<byte> frame)
    {
        var start = output.Position;
        output.Write("ANMF"u8);
        output.Write([0, 0, 0, 0]);
        output.Write(frame[..16]);
        var data = frame[16..];
        var hasImage = false;
        while (!data.IsEmpty)
        {
            var chunk = ReadChunk(ref data, out var type);
            if (IsPixels(type) || type.SequenceEqual("ALPH"u8))
            {
                hasImage |= IsPixels(type);
                WriteChunk(output, type, chunk);
            }
        }

        if (!hasImage) throw Invalid();
        // Cada chunk interno inclui seu padding, portanto o payload ANMF já é par.
        var end = output.Position;
        BinaryPrimitives.WriteUInt32LittleEndian(output.GetBuffer().AsSpan((int)start + 4), (uint)(end - start - 8));
    }

    private static ReadOnlySpan<byte> ReadChunk(ref ReadOnlySpan<byte> source, out ReadOnlySpan<byte> type)
    {
        if (source.Length < 8) throw Invalid();
        var size = BinaryPrimitives.ReadUInt32LittleEndian(source[4..]);
        var padded = (ulong)size + (size & 1);
        if (padded > (ulong)(source.Length - 8)) throw Invalid();
        type = source[..4];
        var data = source.Slice(8, (int)size);
        source = source[(8 + (int)padded)..];
        return data;
    }

    private static bool IsPixels(ReadOnlySpan<byte> type) => type.SequenceEqual("VP8 "u8) || type.SequenceEqual("VP8L"u8);

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> payload)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(length, (uint)payload.Length);
        output.Write(type);
        output.Write(length);
        output.Write(payload);
        if ((payload.Length & 1) != 0) output.WriteByte(0);
    }

    private static InvalidDataException Invalid() => new("Estrutura WebP incompleta.");
}
