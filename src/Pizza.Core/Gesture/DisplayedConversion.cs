namespace Pizza.Core.Gesture;

/// <summary>Qual conversão o disco está mostrando, para o resultado de outra não tomar o lugar dela.</summary>
public sealed class DisplayedConversion
{
    private long _lastId;
    private long? _displayed;

    public long Begin()
    {
        _lastId++;
        _displayed = _lastId;
        return _lastId;
    }

    public void Clear() => _displayed = null;

    /// <summary>Verdadeiro se o resultado é da conversão na tela; ela deixa de estar em andamento.</summary>
    public bool TryFinish(long id)
    {
        if (_displayed != id)
        {
            return false;
        }

        _displayed = null;
        return true;
    }
}
