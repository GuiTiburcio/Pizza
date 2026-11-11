namespace Pizza.Core.Ipc;

/// <summary>
/// Liga o aviso a uma conversão em andamento (ADR-029): o aviso pede a pausa e recebe a porcentagem do Worker.
/// A interface e a thread da conversão usam o mesmo objeto, por isso a pausa é volátil.
/// </summary>
public sealed class ConversionControl
{
    private volatile bool _paused;

    public bool Paused
    {
        get => _paused;
        set => _paused = value;
    }

    /// <summary>Chamado na thread da conversão a cada porcentagem nova, já validada (0 a 100).</summary>
    public Action<int>? Progress { get; init; }
}
