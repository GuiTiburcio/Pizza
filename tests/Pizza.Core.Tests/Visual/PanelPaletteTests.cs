using Pizza.Core.Visual;

namespace Pizza.Core.Tests.Visual;

/// <summary>Fase 4b / NFR-06: contraste dos pares realmente usados pelo painel.</summary>
public class PanelPaletteTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Texto_inclusive_secundario_e_selecao_atende_45_para_1(bool dark)
    {
        var palette = dark ? PanelPalette.Dark : PanelPalette.Light;
        (uint Text, uint Background)[] pairs =
        [
            (palette.Text, palette.Background),
            (palette.Text, palette.Surface),
            (palette.SecondaryText, palette.Background),
            (palette.OnAccent, palette.Accent),
        ];

        foreach (var (text, background) in pairs)
        {
            var a = Luminance(text);
            var b = Luminance(background);
            var contrast = (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
            Assert.True(contrast >= 4.5, $"Tema escuro={dark}, contraste={contrast:F4}");
        }
    }

    // sRGB/WCAG. COLORREF tem os bytes na ordem R, G, B.
    private static double Luminance(uint color) =>
        (0.2126 * Linear(color & 255)) +
        (0.7152 * Linear((color >> 8) & 255)) +
        (0.0722 * Linear((color >> 16) & 255));

    private static double Linear(uint channel)
    {
        var value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
