namespace Pizza.Core.Gesture;

/// <summary>Estados do gesto (Parte 6): repouso, arrastando sem menu e menu aberto.</summary>
public enum GestureState
{
    Idle,
    WaitingForFile,
    Menu,
    Choosing,
}

/// <summary>O que o overlay deve fazer depois de um evento do arraste.</summary>
public enum SessionAction
{
    None,
    ShowMenu,

    /// <summary>Saiu de uma janela nossa: espera um instante para ver se entrou na outra.</summary>
    WaitForReentry,

    /// <summary>O botão subiu: a camada fica mais um instante, até o Windows concluir o arraste.</summary>
    WaitForDrop,
    Close,
    PrepareChoice,
}

/// <summary>
/// Decide o que fazer com os eventos do arraste OLE. O menu só abre quando o OLE confirma que há
/// arquivos (SEC-06), e o cancelamento vem do próprio OLE, sem captura de teclado (SEC-03).
/// </summary>
public sealed class GestureSession
{
    private bool _waitingReentry;
    private bool _released;

    public GestureState State { get; private set; }

    public void Begin()
    {
        State = GestureState.WaitingForFile;
        _waitingReentry = false;
        _released = false;
    }

    public void End()
    {
        State = GestureState.Idle;
        _waitingReentry = false;
        _released = false;
    }

    /// <summary>
    /// O hook vê o botão subir antes de o Windows concluir o arraste. Em qualquer estado ativo,
    /// a camada precisa continuar na tela para o drop não cair na janela de baixo.
    /// </summary>
    public SessionAction OnButtonReleased()
    {
        if (State is GestureState.Idle or GestureState.Choosing)
        {
            return SessionAction.None;
        }

        _released = true;
        return SessionAction.WaitForDrop;
    }

    public SessionAction OnDragEnter(bool hasFiles)
    {
        if (State is GestureState.Idle or GestureState.Choosing)
        {
            return SessionAction.None;
        }

        // Com o botão já solto, não faz sentido abrir o menu: o arraste está terminando.
        if (_released && State == GestureState.WaitingForFile)
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
        if (State is GestureState.Idle or GestureState.Choosing)
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

    /// <summary>ADR-025: só um drop OLE confirmado prepara a escolha por clique.</summary>
    public SessionAction OnDrop(bool hasFile)
    {
        if (State is GestureState.Idle or GestureState.Choosing)
        {
            return SessionAction.None;
        }

        if (State != GestureState.Menu || !hasFile)
        {
            End();
            return SessionAction.Close;
        }

        _waitingReentry = false;
        _released = false;
        State = GestureState.Choosing;
        return SessionAction.PrepareChoice;
    }

    public SessionAction OnDropTimeout()
    {
        if (State is GestureState.Idle or GestureState.Choosing)
        {
            return SessionAction.None;
        }

        End();
        return SessionAction.Close;
    }
}
