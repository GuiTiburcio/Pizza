using Pizza.Core.Gesture;

namespace Pizza.Core.Visual;

/// <summary>
/// Posições do menu na janela, em pixels a 96 dpi (WF-1): barra de abas acima do disco,
/// disco de 380 px e a linha de dica abaixo.
/// </summary>
public static class DiscLayout
{
    public const int Width = 400;
    public const int TabBarHeight = 54;
    public const int BarGap = 10;
    public const int DiscTop = TabBarHeight + BarGap;
    public const int CenterX = Width / 2;
    public const int CenterY = DiscTop + 190;
    public const int HintTop = CenterY + 190 + 12;
    public const int HintHeight = 32;
    public const int HintLeft = 10;
    public const int HintWidth = Width - 20;
    public const int Height = HintTop + HintHeight;

    public const int TabWidth = 84;
    public const int TabPadding = 5;
    public const int TabBarWidth = (TabWidth * 4) + (TabPadding * 2);
    public const int TabBarLeft = (Width - TabBarWidth) / 2;

    public static (double Left, double Right) TabSpan(int tab)
    {
        var left = TabBarLeft + TabPadding + (tab * TabWidth);
        return (left, left + TabWidth);
    }

    /// <summary>Destaque da aba ativa na posição contínua do carrossel, dando a volta como a roda.</summary>
    public static double HighlightLeft(double position)
    {
        var count = MenuTabs.Names.Count;
        var wrapped = ((position % count) + count) % count;
        var from = (int)Math.Floor(wrapped);
        var to = (from + 1) % count;
        var amount = wrapped - from;
        return TabSpan(from).Left + ((TabSpan(to).Left - TabSpan(from).Left) * amount);
    }
}
