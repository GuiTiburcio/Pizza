namespace Pizza.Core.Visual;

/// <summary>
/// ADR-037: a barra de abas e a faixa seguem o disco com atraso suave. Cada quadro anda uma parte do caminho
/// (constante de tempo de 40 ms) e, a menos de meio pixel, encaixa no destino.
/// </summary>
public static class BarFollow
{
    private const double TimeConstantMs = 40;
    private const double Snap = 0.5;

    public static double Step(double position, double goal, double elapsedMs)
    {
        var next = position + ((goal - position) * (1 - Math.Exp(-Math.Max(0, elapsedMs) / TimeConstantMs)));
        return Math.Abs(goal - next) < Snap ? goal : next;
    }
}
