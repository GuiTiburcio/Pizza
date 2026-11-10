namespace Pizza.Core.Visual;

/// <summary>
/// Desfoque do vidro (ADR-023): três passadas de média em cada direção, que se aproximam de um gaussiano,
/// seguidas de saturação. Trabalha em BGRA, no lugar, e deixa todos os pixels opacos.
/// </summary>
public static class GlassBlur
{
    private const int Passes = 3;

    public static void Apply(Span<byte> bgra, int width, int height, int radius, double saturation)
    {
        var line = new int[Math.Max(width, height) * 3];
        for (var pass = 0; pass < Passes; pass++)
        {
            Blur(bgra, width, height, radius, horizontal: true, line);
            Blur(bgra, width, height, radius, horizontal: false, line);
        }

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
