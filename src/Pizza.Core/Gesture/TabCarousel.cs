namespace Pizza.Core.Gesture;

/// <summary>
/// Posição animada das abas do menu: uma mola sem quique que persegue o destino.
/// A mola carrega a velocidade quando o destino muda no meio do caminho, então girar
/// a roda rápido dá um deslize contínuo, e a fórmula exata não depende da taxa de quadros.
/// </summary>
public sealed class TabCarousel(int count)
{
    /// <summary>Um "dente" da roda. Touchpads e rodas de alta precisão enviam frações disso.</summary>
    private const int WheelNotch = 120;

    /// <summary>
    /// Duração da troca de aba. Com esta mola, metade do caminho sai em cerca de 64 ms e 98% em cerca de 222 ms:
    /// dá para perceber o giro sem esperar por ele (pedido do mantenedor em 10/11/2026; era 180 ms).
    /// </summary>
    private const double DurationMs = 240;

    private const double AngularFrequency = 2 * Math.PI / DurationMs;
    private const double SnapDistance = 0.002;
    private const double SnapVelocity = 0.0005;

    private int _accumulatedDelta;
    private int _goal;
    private double _velocity;

    /// <summary>Posição contínua e sem volta: 1,5 é o meio do caminho entre as abas 1 e 2.</summary>
    public double Position { get; private set; }

    /// <summary>Índice da aba de destino, de 0 a count - 1.</summary>
    public int Target => ((_goal % count) + count) % count;

    public bool IsAnimating => Position != _goal;

    public void Reset(int? pageCount = null)
    {
        if (pageCount is { } pages)
        {
            count = pages;
        }

        _accumulatedDelta = 0;
        _goal = 0;
        _velocity = 0;
        Position = 0;
    }

    /// <summary>
    /// Registra um giro da roda. Para baixo (delta negativo) avança, para cima volta.
    /// Só troca de aba ao acumular um dente inteiro; inverter o sentido descarta o acumulado.
    /// </summary>
    /// <returns>Verdadeiro se o destino mudou.</returns>
    public bool OnWheel(int delta)
    {
        if (delta == 0)
        {
            return false;
        }

        if (Math.Sign(delta) != Math.Sign(_accumulatedDelta))
        {
            _accumulatedDelta = 0;
        }

        _accumulatedDelta += delta;
        var notches = _accumulatedDelta / WheelNotch;
        if (notches == 0)
        {
            return false;
        }

        _accumulatedDelta -= notches * WheelNotch;
        _goal -= notches;
        return true;
    }

    /// <summary>Avança a animação pelo tempo decorrido, com a solução exata da mola criticamente amortecida.</summary>
    public void Advance(double elapsedMs)
    {
        var offset = Position - _goal;
        var term = (_velocity + (AngularFrequency * offset)) * elapsedMs;
        var decay = Math.Exp(-AngularFrequency * elapsedMs);

        Position = _goal + ((offset + term) * decay);
        _velocity = (_velocity - (AngularFrequency * term)) * decay;

        if (Math.Abs(Position - _goal) < SnapDistance && Math.Abs(_velocity) < SnapVelocity)
        {
            Snap();
        }
    }

    /// <summary>Vai direto ao destino, sem animação.</summary>
    public void Snap()
    {
        Position = _goal;
        _velocity = 0;
    }

    /// <summary>
    /// Distância com sinal da aba até a posição atual, pelo caminho mais curto do círculo.
    /// Zero é a aba centralizada; 1 é a vizinha seguinte.
    /// </summary>
    public double DistanceTo(int tab)
    {
        var distance = (tab - Position) % count;
        if (distance > count / 2.0)
        {
            return distance - count;
        }

        return distance < -count / 2.0 ? distance + count : distance;
    }
}
