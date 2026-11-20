namespace Pizza.Core.Visual;

public enum GripZone
{
    Target,
    Move,
    Resize,
}

/// <summary>Direção da seta de tamanho, apontando para fora do centro.</summary>
public enum ResizeDirection
{
    Horizontal,
    Vertical,
    Diagonal,
    AntiDiagonal,
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

        return onTarget ? GripZone.Target : GripZone.Move;
    }

    /// <summary>Barra de abas e faixa: as abas não são botões, então a barra inteira move; o nível é alvo.</summary>
    public static GripZone Bar(bool onLevel) => onLevel ? GripZone.Target : GripZone.Move;

    /// <summary>Seta de tamanho na direção do cursor a partir do centro, em oito rumos (y para baixo).</summary>
    public static ResizeDirection Direction(double dx, double dy)
    {
        var angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        var sector = (int)Math.Round((((angle % 180) + 180) % 180) / 45) % 4;
        return sector switch
        {
            0 => ResizeDirection.Horizontal,
            1 => ResizeDirection.Diagonal,
            2 => ResizeDirection.Vertical,
            _ => ResizeDirection.AntiDiagonal,
        };
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
