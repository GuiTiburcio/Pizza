namespace Pizza.Core.Visual;

/// <summary>
/// Posição da bolha de vidro (ADR-027): a mesma mola sem quique do giro de aba (240 ms), que carrega a
/// velocidade quando o destino muda no meio do caminho. A posição é contínua: 1,5 fica entre os itens 1 e 2.
/// </summary>
public sealed class GlideSpring
{
    private const double DurationMs = 240;
    private const double AngularFrequency = 2 * Math.PI / DurationMs;
    private const double SnapDistance = 0.002;
    private const double SnapVelocity = 0.0005;

    private double _from;
    private double _velocity;

    public double Position { get; private set; }

    public double Goal { get; private set; }

    public bool IsAnimating => Position != Goal;

    /// <summary>De 0 nas pontas a 1 no meio do caminho: a bolha cresce levemente enquanto passa.</summary>
    public double Swell => Goal == _from ? 0 : Math.Sin(Math.PI * Math.Clamp((Position - _from) / (Goal - _from), 0, 1));

    public void Retarget(double goal)
    {
        if (goal == Goal)
        {
            return;
        }

        _from = Position;
        Goal = goal;
    }

    /// <summary>Itens em círculo, como as fatias: vai pelo lado mais curto.</summary>
    public void RetargetOnRing(int item, int count)
    {
        var delta = (((item - Goal) % count) + count) % count;
        Retarget(Goal + (delta > count / 2.0 ? delta - count : delta));
    }

    public void Snap(double goal)
    {
        Goal = goal;
        Position = goal;
        _from = goal;
        _velocity = 0;
    }

    /// <summary>Solução exata da mola criticamente amortecida, independente da taxa de quadros.</summary>
    public void Advance(double elapsedMs)
    {
        var offset = Position - Goal;
        var term = (_velocity + (AngularFrequency * offset)) * elapsedMs;
        var decay = Math.Exp(-AngularFrequency * elapsedMs);

        Position = Goal + ((offset + term) * decay);
        _velocity = (_velocity - (AngularFrequency * term)) * decay;

        if (Math.Abs(Position - Goal) < SnapDistance && Math.Abs(_velocity) < SnapVelocity)
        {
            Position = Goal;
            _from = Goal;
            _velocity = 0;
        }
    }
}
