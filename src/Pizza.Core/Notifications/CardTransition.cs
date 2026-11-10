namespace Pizza.Core.Notifications;

/// <summary>
/// Um valor animado do cartão. É uma transição, não uma animação fixa: mudar o destino continua
/// de onde está, como pede a receita de toast da skill animate (400 ms, curva "ease").
/// </summary>
public sealed class CardTransition(double value, double durationMs = 400)
{
    private readonly double DurationMs = durationMs;

    // A curva "ease" do CSS: cubic-bezier(0.25, 0.1, 0.25, 1).
    private const double X1 = 0.25;
    private const double Y1 = 0.1;
    private const double X2 = 0.25;
    private const double Y2 = 1;
    private const int SolveSteps = 32;

    private double _from = value;
    private double _to = value;
    private double _elapsedMs = durationMs;

    public double Value => _from + ((_to - _from) * Ease(_elapsedMs / DurationMs));

    public bool IsDone => _elapsedMs >= DurationMs;

    public static double Ease(double time)
    {
        if (time <= 0 || time >= 1)
        {
            return time <= 0 ? 0 : 1;
        }

        var low = 0.0;
        var high = 1.0;
        for (var step = 0; step < SolveSteps; step++)
        {
            var middle = (low + high) / 2;
            if (Bezier(middle, X1, X2) < time)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return Bezier((low + high) / 2, Y1, Y2);
    }

    public void Retarget(double target)
    {
        if (target.Equals(_to))
        {
            return;
        }

        _from = Value;
        _to = target;
        _elapsedMs = 0;
    }

    public void Advance(double elapsedMs) => _elapsedMs = Math.Min(DurationMs, _elapsedMs + elapsedMs);

    public void Snap(double target)
    {
        _from = target;
        _to = target;
        _elapsedMs = DurationMs;
    }

    private static double Bezier(double t, double p1, double p2)
    {
        var inverse = 1 - t;
        return (3 * inverse * inverse * t * p1) + (3 * inverse * t * t * p2) + (t * t * t);
    }
}
