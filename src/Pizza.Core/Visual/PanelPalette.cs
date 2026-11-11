namespace Pizza.Core.Visual;

/// <summary>Cores do texto, da borda e do destaque do painel, em COLORREF. O fundo do painel é vidro (ADR-027).</summary>
public readonly record struct PanelPalette(
    uint Background, uint Surface, uint Text, uint SecondaryText, uint Accent, uint OnAccent, uint Border)
{
    public static PanelPalette Light { get; } = new(
        Rgb(245, 246, 250), Rgb(235, 237, 245), Rgb(15, 19, 32), Rgb(31, 38, 56),
        Rgb(47, 106, 255), Rgb(255, 255, 255), Rgb(203, 210, 228));

    public static PanelPalette Dark { get; } = new(
        Rgb(26, 33, 64), Rgb(62, 72, 112), Rgb(255, 255, 255), Rgb(170, 182, 214),
        Rgb(47, 106, 255), Rgb(255, 255, 255), Rgb(84, 96, 136));

    // Um nível abaixo de #2F6BFF no verde: branco sobre o token original dá 4,4988:1.
    // #2F6AFF mantém o azul do projeto e passa o mínimo de 4,5:1 do NFR-06.
    private static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));
}
