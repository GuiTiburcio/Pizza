using System.Runtime.Versioning;

namespace Pizza.Core.Ipc;

/// <summary>
/// Worker de reserva (adendo de 13/11 à ADR-033, NFR-05): abre quando o menu abre, para que os ~170 ms de iniciar
/// o processo isolado passem enquanto a pessoa escolhe a fatia. Nada muda no isolamento: é o mesmo Worker em
/// AppContainer (SEC-20), que recebe só handles (SEC-15) e serve um único arquivo (SEC-29). Sem pedido dentro de
/// <paramref name="idle"/>, a reserva é encerrada.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WorkerSpare(Func<SandboxedProcess> start, TimeSpan idle) : IDisposable
{
    private readonly Lock _gate = new();
    private SandboxedProcess? _spare;
    private Timer? _expiry;

    /// <summary>Há uma reserva viva esperando o próximo arquivo.</summary>
    public bool IsWarm
    {
        get
        {
            lock (_gate)
            {
                return _spare is { } spare && !spare.WaitForExit(0);
            }
        }
    }

    /// <summary>Abre uma reserva, se ainda não houver uma viva.</summary>
    public void WarmUp()
    {
        lock (_gate)
        {
            if (_spare is { } spare && !spare.WaitForExit(0))
            {
                return;
            }

            Release();
            var started = start();
            _spare = started;
            _expiry = new Timer(_ => Expire(started), null, idle, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Entrega a reserva, que deixa de ser reserva; sem uma viva, abre um Worker na hora, como antes.</summary>
    public SandboxedProcess Take()
    {
        lock (_gate)
        {
            if (_spare is { } spare && !spare.WaitForExit(0))
            {
                _expiry?.Dispose();
                _expiry = null;
                _spare = null;
                return spare;
            }

            Release();
        }

        return start();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            Release();
        }
    }

    private void Expire(SandboxedProcess spare)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_spare, spare))
            {
                Release();
            }
        }
    }

    /// <summary>Fechar o Job encerra o processo (KILL_ON_JOB_CLOSE).</summary>
    private void Release()
    {
        _expiry?.Dispose();
        _expiry = null;
        _spare?.Dispose();
        _spare = null;
    }
}
