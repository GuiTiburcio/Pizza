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

    // ADR-027: faixa de níveis entre a barra e o disco. Título em cima e até quatro níveis (prancha 4b-1).
    public const int StripTop = TabBarHeight + BarGap;
    public const int StripTitleHeight = 22;
    public const int StripTitleWidth = 180;
    public const int StripTitleGap = 8;
    public const int LevelTop = StripTop + StripTitleHeight + StripTitleGap;
    public const int LevelHeight = 44;
    public const int LevelWidth = 84;
    public const int LevelGap = 6;
    public const int StripHeight = StripTitleHeight + StripTitleGap + LevelHeight;

    public const int DiscTop = StripTop + StripHeight + BarGap;
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

    public static (double Left, double Right) LevelSpan(int level, int count)
    {
        var start = (Width - ((count * LevelWidth) + ((count - 1) * LevelGap))) / 2.0;
        var left = start + (level * (LevelWidth + LevelGap));
        return (left, left + LevelWidth);
    }

    /// <summary>Nível sob o cursor, em coordenadas da janela a 96 dpi.</summary>
    public static int? LevelAt(double x, double y, int count)
    {
        if (y < LevelTop || y > LevelTop + LevelHeight)
        {
            return null;
        }

        for (var level = 0; level < count; level++)
        {
            var (left, right) = LevelSpan(level, count);
            if (x >= left && x <= right)
            {
                return level;
            }
        }

        return null;
    }

    /// <summary>Faixa inteira, inclusive o título e os vãos: com o cursor aqui, a roda troca o nível.</summary>
    public static bool InStrip(double x, double y) =>
        x >= 0 && x <= Width && y >= StripTop && y <= StripTop + StripHeight;

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
