namespace Pizza.Converters.Docs;

/// <summary>Conserva imagens, paletas e controles de animação do GIF, sem extensões de comentários ou XMP.</summary>
internal static class GifMetadata
{
    public static byte[] Remove(byte[] image)
    {
        var data = image.AsSpan();
        if (data.Length < 13 || (!data.StartsWith("GIF87a"u8) && !data.StartsWith("GIF89a"u8)))
        {
            throw new InvalidDataException("Cabeçalho GIF inválido.");
        }

        var position = 13 + PaletteLength(data[10]);
        var kept = position;
        var hasGraphic = false;
        while (position < data.Length)
        {
            var start = position;
            var marker = data[position++];
            var keep = true;
            switch (marker)
            {
                case 0x2c: // Descritor, paleta local e tamanho de código LZW.
                    if (data.Length - position < 9) throw Invalid();
                    position += 10 + PaletteLength(data[position + 8]);
                    hasGraphic = true;
                    break;
                case 0x21:
                    if (position >= data.Length) throw Invalid();
                    var label = data[position++];
                    if (label == 0xff) // Aplicativo: só perfil ICC e extensão de repetição.
                    {
                        var app = data[position..];
                        if (app.Length < 12 || app[0] != 11) throw Invalid();
                        var id = app.Slice(1, 11);
                        keep = id.SequenceEqual("ICCRGBG1012"u8)
                            || (app.Length >= 17 && (id.SequenceEqual("NETSCAPE2.0"u8) || id.SequenceEqual("ANIMEXTS1.0"u8))
                                && app[12] == 3 && app[13] == 1 && app[16] == 0);
                    }
                    else
                    {
                        // Texto simples é um elemento visível. Controle gráfico mantém transparência e atraso.
                        keep = label is 0x01 or 0xf9;
                        if (label == 0xf9 && (data.Length - position < 6 || data[position] != 4 || data[position + 5] != 0))
                            throw Invalid();
                        if (label == 0x01 && (data.Length - position < 13 || data[position] != 12))
                            throw Invalid();
                        hasGraphic |= label == 0x01;
                    }

                    break;
                case 0x3b when hasGraphic:
                    data[kept++] = marker;
                    return kept == image.Length ? image : image[..kept];
                default:
                    throw Invalid();
            }

            position = SkipSubBlocks(data, position);
            if (keep)
            {
                data[start..position].CopyTo(data[kept..]);
                kept += position - start;
            }
        }

        throw Invalid();
    }

    private static int PaletteLength(byte flags) => (flags & 0x80) == 0 ? 0 : 3 << ((flags & 7) + 1);

    private static int SkipSubBlocks(ReadOnlySpan<byte> data, int position)
    {
        while (position < data.Length)
        {
            var length = data[position++];
            if (length == 0) return position;
            if (length > data.Length - position) throw Invalid();
            position += length;
        }

        throw Invalid();
    }

    private static InvalidDataException Invalid() => new("Estrutura GIF incompleta.");
}
