using Pizza.App.Interop;
using Pizza.Core.Diagnostics;
using Pizza.Core.Gesture;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>Arraste e soltura: eventos do OLE, drop numa fatia ou fora dela e fim do gesto (SEC-05, SEC-06, SEC-31).</summary>
internal static unsafe partial class Overlay
{
    public static void OnDragEnter(DraggedFiles? files, int x, int y)
    {
        if (Session.State == GestureState.Choosing)
        {
            return;
        }

        KillTimer(Disc, TimerLeave);
        var action = Session.OnDragEnter(files is { } candidate && RadialMenu.CanConvert(candidate.First));
        if (action == SessionAction.Close)
        {
            // Texto, ou arquivo sem conversão (FR-09): sai da frente e deixa o arraste seguir normal.
            Log.Write(LogEvent.NotAFile);
            Hide();
            return;
        }

        if (files is { } dragged && _files?.First != dragged.First)
        {
            _files = files;
        }

        if (action == SessionAction.ShowMenu)
        {
            ShowMenu(x, y);
        }
        else
        {
            Render();
        }
    }

    public static void OnDragLeave()
    {
        if (Session.OnDragLeave() == SessionAction.WaitForReentry)
        {
            SetTimer(Disc, TimerLeave, LeaveGraceMs, 0);
        }
    }

    /// <summary>Atualiza o destaque e diz se o cursor está sobre uma fatia que aceita o arquivo.</summary>
    public static bool OnDragOver(int x, int y)
    {
        var slot = TargetSlot(x, y);
        if (slot != _hoverSlot)
        {
            SetHover(slot);
        }

        // ADR-025 / SEC-05: qualquer posição prepara o arquivo; só uma fatia seleciona o formato.
        return Session.State == GestureState.Menu && _files is { Count: 1 } && IsVisible && _pulseSlot is null;
    }

    /// <summary>Aceita o drop numa fatia e mostra o pulso antes de seguir. Devolve falso se o drop não vale.</summary>
    public static bool OnDrop(DraggedFiles? files, int x, int y, bool selectSlice = true)
    {
        // CF_HDROP é relido na soltura. Validar antes de alterar o estado ou indexar o catálogo (SEC-06).
        if (Session.State != GestureState.Menu || files is not { Count: 1 } file ||
            _files is not { Count: 1 } entered || !string.Equals(file.First, entered.First, StringComparison.OrdinalIgnoreCase) ||
            !RadialMenu.CanConvert(file.First) || !IsVisible || _pulseSlot is not null)
        {
            return false;
        }

        var slot = selectSlice ? TargetSlot(x, y) : null;
        if (slot is { } selected)
        {
            return Select(file.First, selected);
        }

        if (Session.OnDrop(true) != SessionAction.PrepareChoice)
        {
            return false;
        }

        StopDragTimers();
        _clickArmed = false;
        // Só ativa a janela depois que Drop devolver o controle ao Explorer.
        PostMessageW(Disc, WmChoiceReady, 0, 0);
        return true;
    }

    private static bool Select(string path, int slot)
    {
        var page = _pages[Carousel.Target];
        var converter = RadialMenu.Slices(page.Tab, path, page.Turn).ElementAtOrDefault(slot);
        if (converter is null)
        {
            return false;
        }

        Log.Write(LogEvent.FileDropped);
        Session.End();
        StopDragTimers();
        ShowWindow(_catcher, SW_HIDE);

        // O resto acontece depois que o Drop retornar: o Explorer fica preso até lá.
        _dropped = (path, converter);
        _pulseSlot = slot;
        Render();
        SetTimer(Disc, TimerPulse, _motion == MotionStyle.Instant ? 1 : PulseMs, 0);
        return true;
    }

    private static void StopDragTimers()
    {
        KillTimer(Disc, TimerAfterRelease);
        KillTimer(Disc, TimerWatchdog);
        KillTimer(Disc, TimerLeave);
        KillTimer(Disc, TimerChoice);
    }

    private static void FinishDrop()
    {
        KillTimer(Disc, TimerPulse);
        if (_dropped is not { } dropped)
        {
            return;
        }

        _dropped = null;
        _pulseSlot = null;
        if (dropped.Converter.HasPanel)
        {
            _awaitingOptions = dropped;
            var (centerX, centerY) = DiscCenter();
            HideWindow();
            OptionsPanel.Show(dropped.Converter, Path.GetFileName(dropped.Path), centerX, centerY);
        }
        else
        {
            Hide();
            Notifications.Convert(dropped.Path, dropped.Converter, new Dictionary<string, string>());
        }
    }

    /// <summary>O painel fechou: com as opções escolhidas, ou null se o usuário cancelou.</summary>
    public static void OnPanelClosed(IReadOnlyDictionary<string, string>? options)
    {
        if (_awaitingOptions is not { } pending)
        {
            return;
        }

        _awaitingOptions = null;
        Hide();
        if (options is null)
        {
            Log.Write(LogEvent.MenuCancelled);
            return;
        }

        Notifications.Convert(pending.Path, pending.Converter, options);
    }

    public static void OnDropOutside()
    {
        Log.Write(LogEvent.MenuCancelled);
        Hide();
    }

    /// <summary>Estado 2: só a camada invisível, até o OLE confirmar que o arraste é de arquivos.</summary>
    private static void BeginGesture()
    {
        _dropDeadline = 0;
        OptionsPanel.Close();
        _awaitingOptions = null;
        _dropped = null;
        _pulseSlot = null;
        KillTimer(Disc, TimerAfterRelease);
        KillTimer(Disc, TimerPulse);
        HideWindow();
        _files = null;
        Session.Begin();

        SetWindowPos(
            _catcher,
            HWND_TOPMOST,
            GetSystemMetrics(SM_XVIRTUALSCREEN),
            GetSystemMetrics(SM_YVIRTUALSCREEN),
            GetSystemMetrics(SM_CXVIRTUALSCREEN),
            GetSystemMetrics(SM_CYVIRTUALSCREEN),
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
        SetTimer(Disc, TimerWatchdog, WatchdogMs, 0);
    }

    private static void OnButtonReleased()
    {
        KillTimer(Disc, TimerWatchdog);
        if (Session.OnButtonReleased() == SessionAction.WaitForDrop)
        {
            _dropDeadline = Environment.TickCount64 + AfterReleaseMs;
            if (Disc != 0) SetTimer(Disc, TimerAfterRelease, AfterReleaseMs, 0);
        }
    }

    private static bool IsPrimaryButtonDown()
    {
        // GetAsyncKeyState lê o botão físico; com os botões trocados no Windows, o principal é o direito.
        var button = GetSystemMetrics(SM_SWAPBUTTON) != 0 ? VK_RBUTTON : VK_LBUTTON;
        return (GetAsyncKeyState(button) & 0x8000) != 0;
    }
}
