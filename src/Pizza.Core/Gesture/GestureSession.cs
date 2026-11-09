namespace Pizza.Core.Gesture;

/// <summary>Estados do gesto (Parte 6): repouso, arrastando sem menu e menu aberto.</summary>
public enum GestureState
{
    Idle,
    WaitingForFile,
    Menu,
}

/// <summary>O que o overlay deve fazer depois de um evento do arraste.</summary>
public enum SessionAction
{
    None,
    ShowMenu,

    /// <summary>Saiu de uma janela nossa: espera um instante para ver se entrou na outra.</summary>
    WaitForReentry,
    Close,
}

/// <summary>
/// Decide o que fazer com os eventos do arraste OLE. O menu só abre quando o OLE confirma que há
/// arquivos (SEC-06), e o cancelamento vem do próprio OLE, sem captura de teclado (SEC-03).
/// </summary>
public sealed class GestureSession
{
    private bool _waitingReentry;

    public GestureState State { get; private set; }

    public void Begin()
    {
        State = GestureState.WaitingForFile;
        _waitingReentry = false;
    }

    public void End()
    {
        State = GestureState.Idle;
        _waitingReentry = false;
    }

    public SessionAction OnDragEnter(bool hasFiles)
    {
        if (State == GestureState.Idle)
        {
            return SessionAction.None;
        }

        _waitingReentry = false;
        if (!hasFiles)
        {
            End();
            return SessionAction.Close;
        }

        if (State == GestureState.Menu)
        {
            return SessionAction.None;
        }

        State = GestureState.Menu;
        return SessionAction.ShowMenu;
    }

    public SessionAction OnDragLeave()
    {
        if (State == GestureState.Idle)
        {
            return SessionAction.None;
        }

        _waitingReentry = true;
        return SessionAction.WaitForReentry;
    }

    /// <summary>Terminou a espera: sem nova entrada, o arraste foi cancelado (Esc).</summary>
    public SessionAction OnLeaveTimeout()
    {
        if (!_waitingReentry)
        {
            return SessionAction.None;
        }

        End();
        return SessionAction.Close;
    }
}
