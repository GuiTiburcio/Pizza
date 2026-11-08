namespace Pizza.Core.Gesture;

/// <summary>
/// Posição animada das abas do menu. A posição persegue o alvo com decaimento exponencial:
/// não depende da taxa de quadros e pode mudar de alvo no meio do caminho sem tranco,
/// o que mantém o deslize suave mesmo com a roda girando rápido.
/// </summary>
public sealed class TabCarousel(int count)
{
    /// <summary>Um "dente" da roda. Touchpads e rodas de alta precisão enviam frações disso.</summary>
    private const int WheelNotch = 120;

    /// <summary>Com 45 ms, o deslize chega a 98% do caminho em cerca de 180 ms (token de animação, Parte 4.2).</summary>
    private const double TimeConstantMs = 45;

    private const double SnapDistance = 0.002;

    private int _accumulatedDelta;
    private int _goal;

    /// <summary>Posição contínua e sem volta: 1,5 é o meio do caminho entre as abas 1 e 2.</summary>
    public double Position { get; private set; }

    /// <summary>Índice da aba de destino, de 0 a count - 1.</summary>
    public int Target => ((_goal % count) + count) % count;

    public bool IsAnimating => Position != _goal;

    public void Reset()
    {
        _accumulatedDelta = 0;
        _goal = 0;
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

    /// <summary>Avança a animação pelo tempo decorrido desde o quadro anterior.</summary>
    public void Advance(double elapsedMs)
    {
        Position += (_goal - Position) * (1 - Math.Exp(-elapsedMs / TimeConstantMs));
        if (Math.Abs(_goal - Position) < SnapDistance)
        {
            Position = _goal;
        }
    }

    /// <summary>Vai direto ao destino, sem animação ("Reduzir animações" do Windows, Parte 2.4).</summary>
    public void Snap() => Position = _goal;

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
