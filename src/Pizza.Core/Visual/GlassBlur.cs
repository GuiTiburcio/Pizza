namespace Pizza.Core.Visual;

/// <summary>
/// Desfoque do vidro (ADR-023): três passadas de média em cada direção, que se aproximam de um gaussiano,
/// seguidas de saturação. Trabalha em BGRA, no lugar, e deixa todos os pixels opacos.
/// </summary>
public static class GlassBlur
{
    private const int Passes = 3;

    // Abaixo disto o desfoque já é barato e a meia resolução não compensa.
    private const int MinimumHalfRadius = 4;

    /// <summary>
    /// NFR-03: desfoca em meia resolução e amplia de volta. O resultado fica igual a olho ao desfoque completo,
    /// porque a imagem já sai borrada, com cerca de um quarto do trabalho.
    /// </summary>
    public static void Apply(Span<byte> bgra, int width, int height, int radius, double saturation)
    {
        if (radius < MinimumHalfRadius || width < 4 || height < 4)
        {
            ApplyFullResolution(bgra, width, height, radius, saturation);
            return;
        }

        var halfWidth = width / 2;
        var halfHeight = height / 2;
        var half = new byte[halfWidth * halfHeight * 4];
        Shrink(bgra, width, half, halfWidth, halfHeight);
        BlurPasses(half, halfWidth, halfHeight, (radius + 1) / 2);
        Enlarge(half, halfWidth, halfHeight, bgra, width, height);
        Saturate(bgra, saturation);
    }

    /// <summary>O desfoque em resolução cheia; referência dos testes e caminho dos raios pequenos.</summary>
    internal static void ApplyFullResolution(Span<byte> bgra, int width, int height, int radius, double saturation)
    {
        BlurPasses(bgra, width, height, radius);
        Saturate(bgra, saturation);
    }

    private static void BlurPasses(Span<byte> bgra, int width, int height, int radius)
    {
        var line = new int[Math.Max(width, height) * 3];
        for (var pass = 0; pass < Passes; pass++)
        {
            Blur(bgra, width, height, radius, horizontal: true, line);
            Blur(bgra, width, height, radius, horizontal: false, line);
        }
    }

    private static void Saturate(Span<byte> bgra, double saturation)
    {
        for (var i = 0; i < bgra.Length; i += 4)
        {
            var gray = (bgra[i] * 0.114) + (bgra[i + 1] * 0.587) + (bgra[i + 2] * 0.299);
            for (var channel = 0; channel < 3; channel++)
            {
                bgra[i + channel] = (byte)Math.Clamp(Math.Round(gray + ((bgra[i + channel] - gray) * saturation)), 0, 255);
            }

            bgra[i + 3] = 255;
        }
    }

    /// <summary>Cada pixel da metade é a média de um bloco 2 × 2 da imagem.</summary>
    private static void Shrink(Span<byte> source, int width, Span<byte> half, int halfWidth, int halfHeight)
    {
        for (var y = 0; y < halfHeight; y++)
        {
            for (var x = 0; x < halfWidth; x++)
            {
                var a = (((2 * y) * width) + (2 * x)) * 4;
                var b = a + (width * 4);
                var target = ((y * halfWidth) + x) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    half[target + channel] = (byte)((source[a + channel] + source[a + 4 + channel] + source[b + channel] + source[b + 4 + channel] + 2) / 4);
                }
            }
        }
    }

    /// <summary>Volta ao tamanho cheio por interpolação bilinear, com a borda repetida.</summary>
    private static void Enlarge(Span<byte> half, int halfWidth, int halfHeight, Span<byte> target, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            var sy = Math.Clamp(((y + 0.5) / 2) - 0.5, 0, halfHeight - 1);
            var y0 = (int)sy;
            var y1 = Math.Min(y0 + 1, halfHeight - 1);
            var fy = sy - y0;
            for (var x = 0; x < width; x++)
            {
                var sx = Math.Clamp(((x + 0.5) / 2) - 0.5, 0, halfWidth - 1);
                var x0 = (int)sx;
                var x1 = Math.Min(x0 + 1, halfWidth - 1);
                var fx = sx - x0;
                var i = ((y * width) + x) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    var top = (half[(((y0 * halfWidth) + x0) * 4) + channel] * (1 - fx)) + (half[(((y0 * halfWidth) + x1) * 4) + channel] * fx);
                    var bottom = (half[(((y1 * halfWidth) + x0) * 4) + channel] * (1 - fx)) + (half[(((y1 * halfWidth) + x1) * 4) + channel] * fx);
                    target[i + channel] = (byte)Math.Round((top * (1 - fy)) + (bottom * fy));
                }
            }
        }
    }

    /// <summary>Média deslizante numa direção, com a borda repetida.</summary>
    private static void Blur(Span<byte> bgra, int width, int height, int radius, bool horizontal, int[] line)
    {
        var length = horizontal ? width : height;
        var lines = horizontal ? height : width;
        var window = (2 * radius) + 1;
        for (var a = 0; a < lines; a++)
        {
            for (var channel = 0; channel < 3; channel++)
            {
                var sum = 0;
                for (var k = -radius; k <= radius; k++)
                {
                    sum += bgra[Index(a, Math.Clamp(k, 0, length - 1), horizontal, width) + channel];
                }

                for (var b = 0; b < length; b++)
                {
                    // Arredonda: truncar escurece a imagem e apaga detalhes a cada passada.
                    line[(b * 3) + channel] = (sum + (window / 2)) / window;
                    sum += bgra[Index(a, Math.Min(length - 1, b + radius + 1), horizontal, width) + channel];
                    sum -= bgra[Index(a, Math.Max(0, b - radius), horizontal, width) + channel];
                }
            }

            for (var b = 0; b < length; b++)
            {
                for (var channel = 0; channel < 3; channel++)
                {
                    bgra[Index(a, b, horizontal, width) + channel] = (byte)line[(b * 3) + channel];
                }
            }
        }
    }

    private static int Index(int line, int position, bool horizontal, int width) =>
        (horizontal ? (line * width) + position : (position * width) + line) * 4;
}
