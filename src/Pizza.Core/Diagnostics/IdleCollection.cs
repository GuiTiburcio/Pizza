namespace Pizza.Core.Diagnostics;

public enum IdleStep
{
    None,

    /// <summary>Volta a contar o tempo até a coleta.</summary>
    Rearm,

    /// <summary>Coleta e devolve a memória do coletor ao Windows.</summary>
    Collect,
}

/// <summary>
/// NFR-01 (Fase 5b): o coletor do .NET mantém reservado o espaço da geração 0, cerca de 4 MB, enchido pelo lixo
/// dos quadros. Depois de um tempo sem mensagens na thread da interface, uma coleta agressiva o devolve, uma vez.
/// </summary>
public sealed class IdleCollection
{
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(10);

    private bool _pending;

    /// <summary>Chamado a cada mensagem da thread da interface; <paramref name="idleTimer"/> é o disparo da espera.</summary>
    public IdleStep OnMessage(bool idleTimer)
    {
        if (!idleTimer)
        {
            _pending = true;
            return IdleStep.Rearm;
        }

        if (!_pending)
        {
            return IdleStep.None;
        }

        _pending = false;
        return IdleStep.Collect;
    }
}
