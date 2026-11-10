using Pizza.Core.Gesture;

namespace Pizza.Core.Visual;

/// <summary>Um arco de círculo: centro, raio e ângulos em graus, no sentido da tela.</summary>
public readonly record struct Arc(double CenterX, double CenterY, double Radius, double Start, double Sweep);

/// <summary>Curva de canto: começo, dois pontos de controle e fim.</summary>
public readonly record struct Corner((double X, double Y) From, (double X, double Y) Control1, (double X, double Y) Control2, (double X, double Y) To);

/// <summary>
/// Contorno de uma fatia, na ordem de desenho: canto, arco externo, canto, lado, canto, arco interno,
/// canto e lado de volta ao começo.
/// </summary>
public sealed record SliceShape(Corner OuterStart, Arc OuterArc, Corner OuterEnd, Corner InnerEnd, Arc InnerArc, Corner InnerStart)
{
    public IEnumerable<(double X, double Y)> AllPoints()
    {
        foreach (var corner in new[] { OuterStart, OuterEnd, InnerEnd, InnerStart })
        {
            yield return corner.From;
            yield return corner.To;
        }
    }
}

/// <summary>
/// Geometria da fatia do WF-1 e do WF-2: r 92 a r 168, 55°, cantos de 9 px. No hover, cresce até 106%
/// a partir do centro do disco e sai 5 px na direção dela.
/// </summary>
public static class SliceGeometry
{
    public const double CornerRadius = 9;
    public const double HoverScale = 0.06;
    public const double HoverShift = 5;

    public static SliceShape Build(double centerDegrees, double hover, double centerX, double centerY)
    {
        var scale = 1 + (HoverScale * hover);
        var direction = Radians(centerDegrees);
        var cx = centerX + (HoverShift * hover * Math.Cos(direction));
        var cy = centerY + (HoverShift * hover * Math.Sin(direction));
        var outer = RadialMenu.OuterRadius * scale;
        var inner = RadialMenu.InnerRadius * scale;
        var corner = CornerRadius * scale;

        var start = centerDegrees - RadialMenu.HalfSliceDegrees;
        var end = centerDegrees + RadialMenu.HalfSliceDegrees;
        var outerOffset = Degrees(corner / outer);
        var innerOffset = Degrees(corner / inner);

        (double X, double Y) At(double radius, double degrees) =>
            (cx + (radius * Math.Cos(Radians(degrees))), cy + (radius * Math.Sin(Radians(degrees))));

        return new SliceShape(
            Round(At(outer - corner, start), At(outer, start), At(outer, start + outerOffset)),
            new Arc(cx, cy, outer, start + outerOffset, (end - outerOffset) - (start + outerOffset)),
            Round(At(outer, end - outerOffset), At(outer, end), At(outer - corner, end)),
            Round(At(inner + corner, end), At(inner, end), At(inner, end - innerOffset)),
            new Arc(cx, cy, inner, end - innerOffset, (start + innerOffset) - (end - innerOffset)),
            Round(At(inner, start + innerOffset), At(inner, start), At(inner + corner, start)));
    }

    /// <summary>Centro do texto da fatia, acompanhando o hover.</summary>
    public static (double X, double Y) LabelCenter(double centerDegrees, double hover, double centerX, double centerY)
    {
        var direction = Radians(centerDegrees);
        var radius = ((RadialMenu.InnerRadius + RadialMenu.OuterRadius) / 2 * (1 + (HoverScale * hover))) + (HoverShift * hover);
        return (centerX + (radius * Math.Cos(direction)), centerY + (radius * Math.Sin(direction)));
    }

    /// <summary>Curva quadrática do canto, escrita como cúbica, que é o que o GDI+ desenha.</summary>
    private static Corner Round((double X, double Y) from, (double X, double Y) control, (double X, double Y) to) =>
        new(from, Lerp(from, control, 2.0 / 3), Lerp(to, control, 2.0 / 3), to);

    private static (double X, double Y) Lerp((double X, double Y) a, (double X, double Y) b, double amount) =>
        (a.X + ((b.X - a.X) * amount), a.Y + ((b.Y - a.Y) * amount));

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    private static double Degrees(double radians) => radians * 180 / Math.PI;
}
