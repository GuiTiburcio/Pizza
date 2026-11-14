namespace Pizza.Core.Visual;

public enum GripZone
{
    Target,
    Move,
    Resize,
}

/// <summary>
/// ADR-037: a borda do círculo ou do retângulo redimensiona; o resto, fora dos alvos, move. Coordenadas em
/// pixels de desenho (96 dpi), relativas à janela.
/// </summary>
public static class MenuGrip
{
    public const double DiscRadius = 190;
    public const double Edge = 8;

    public static GripZone Disc(double x, double y, bool onTarget)
    {
        if (Math.Abs(DiscDistance(x, y) - DiscRadius) <= Edge)
        {
            return GripZone.Resize;
        }

        return onTarget || y <= DiscLayout.TabBarHeight ? GripZone.Target : GripZone.Move;
    }

    public static GripZone Rectangle(double x, double y, double width, double height, bool onTarget)
    {
        if (x <= Edge || y <= Edge || x >= width - Edge || y >= height - Edge)
        {
            return GripZone.Resize;
        }

        return onTarget ? GripZone.Target : GripZone.Move;
    }

    /// <summary>Distância ao centro do disco, para puxar a borda.</summary>
    public static double DiscDistance(double x, double y) =>
        Math.Sqrt(Math.Pow(x - DiscLayout.CenterX, 2) + Math.Pow(y - DiscLayout.CenterY, 2));
}
