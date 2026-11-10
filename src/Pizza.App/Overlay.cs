using System.Diagnostics;
using Pizza.App.Interop;
using Pizza.Core.Conversions;
using Pizza.Core.Diagnostics;
using Pizza.Core.Gesture;
using Pizza.Core.Notifications;
using Pizza.Core.Visual;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// Menu radial no visual do Dossiê (ADR-023). São duas janelas: o menu, que aceita o drop, e uma camada
/// quase invisível sobre todas as telas, que recusa o drop para que soltar fora do menu não vire uma cópia.
/// </summary>
internal static unsafe class Overlay
{
    public const uint WmGestureStarted = WM_APP + 1;
    public const uint WmGestureReleased = WM_APP + 2;
    public const uint WmWheel = WM_APP + 3;

    private const string ClassName = "PizzaOverlay";
    private const nuint TimerAfterRelease = 1;
    private const nuint TimerWatchdog = 3;
    private const nuint TimerAnimation = 4;
    private const nuint TimerLeave = 5;
    private const nuint TimerPulse = 6;
    private const nuint TimerPicking = 7;
    private const uint PickingTimeoutMs = 10_000; // ADR-025: 10 s sem nenhuma interação, o menu fecha.
    private const uint LeaveGraceMs = 80;
    private const uint WatchdogMs = 250;
    private const uint AnimationMs = 10;
    private const uint AfterReleaseMs = 400;
    private const uint PulseMs = 120; // WF-2: pulso ao soltar.
    private const double HoverMs = 180; // WF-2: 180 ms, curva "ease".
    private const double TabRotation = 12; // FR-04: as fatias giram levemente ao trocar de aba.

    // A camada precisa de alfa maior que zero, senão o Windows deixa o mouse atravessar.
    private const byte CatcherAlpha = 1;

    private static readonly DropTarget DiscTarget = new(acceptsDrop: true);
    private static readonly DropTarget CatcherTarget = new(acceptsDrop: false);
    private static readonly TabCarousel Carousel = new(MenuTabs.Names.Count);
    private static readonly GestureSession Session = new();
    private static readonly CardTransition[] Hover = Enumerable.Range(0, RadialMenu.SlotCount).Select(_ => new CardTransition(0, HoverMs)).ToArray();

    private static nint _catcher;
    private static long _lastFrame;
    private static MotionStyle _motion = MotionStyle.Slide;
    private static double _scale = 1;
    private static int _left;
    private static int _top;
    private static int? _hoverSlot;
    private static int? _pulseSlot;
    private static bool _picking;
    private static bool _cancelOnRelease;
    private static DraggedFiles? _files;
    private static (string Path, ConverterInfo Converter)? _dropped;
    private static (string Path, ConverterInfo Converter)? _awaitingOptions;

    /// <summary>Janela do menu; também recebe as mensagens enviadas pelos hooks.</summary>
    public static nint Disc { get; private set; }

    /// <summary>Verdadeiro com o menu na tela; só a camada invisível não conta.</summary>
    public static bool IsVisible { get; private set; }

    public static void Create()
    {
        var instance = GetModuleHandleW(null);
        fixed (char* name = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                CbSize = (uint)sizeof(WNDCLASSEXW),
                LpfnWndProc = &WndProc,
                HInstance = instance,
                HCursor = LoadCursorW(0, IDC_ARROW),
                LpszClassName = name,
            };
            RegisterClassExW(&wc);
        }

        const uint exStyle = WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
        _catcher = CreateWindowExW(exStyle, ClassName, "Pizza Camada", WS_POPUP, 0, 0, 1, 1, 0, 0, instance, 0);
        Disc = CreateWindowExW(exStyle, ClassName, "Pizza", WS_POPUP, 0, 0, DiscLayout.Width, DiscLayout.Height, 0, 0, instance, 0);
        SetLayeredWindowAttributes(_catcher, 0, CatcherAlpha, LWA_ALPHA);

        DiscTarget.Register(Disc);
        CatcherTarget.Register(_catcher);
    }

    public static void Destroy()
    {
        RevokeDragDrop(Disc);
        RevokeDragDrop(_catcher);
    }

    public static void OnDragEnter(DraggedFiles? files, int x, int y)
    {
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

        return slot is not null;
    }

    /// <summary>Aceita o drop numa fatia e mostra o pulso antes de seguir. Devolve falso se o drop não vale.</summary>
    public static bool OnDrop(DraggedFiles? files, int x, int y)
    {
        if (files is not { Count: 1 } file || TargetSlot(x, y) is not { } slot)
        {
            return false;
        }

        KillTimer(Disc, TimerAfterRelease);
        KillTimer(Disc, TimerWatchdog);
        KillTimer(Disc, TimerLeave);
        ShowWindow(_catcher, SW_HIDE);

        // O resto acontece depois que o Drop retornar: o Explorer fica preso até lá.
        StartPulse(file.First, slot);
        return true;
    }

    /// <summary>Fatia escolhida, por drop ou clique: o gesto termina e o pulso confirma antes de seguir.</summary>
    private static void StartPulse(string path, int slot)
    {
        Log.Write(LogEvent.FileDropped);
        Session.End();
        KillTimer(Disc, TimerPicking);
        _dropped = (path, RadialMenu.Slices(Carousel.Target, path)[slot]);
        _pulseSlot = slot;
        Render();
        SetTimer(Disc, TimerPulse, _motion == MotionStyle.Instant ? 1 : PulseMs, 0);
    }

    /// <summary>Dá para escolher com um clique: menu na tela e um arquivo só (FR-30).</summary>
    private static bool CanPick => _files is { Count: 1 } && IsVisible;

    private static void FinishDrop()
    {
        KillTimer(Disc, TimerPulse);
        if (_dropped is not { } dropped)
        {
            return;
        }

        _dropped = null;
        _pulseSlot = null;
        ExitPicking();
        ShowWindow(_catcher, SW_HIDE);
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

    /// <summary>Fatia sob o cursor, se houver uma disponível para o arquivo arrastado.</summary>
    private static int? TargetSlot(int x, int y)
    {
        // FR-30: por enquanto, um arquivo por vez.
        if (_files is not { Count: 1 } file || !IsVisible || _pulseSlot is not null)
        {
            return null;
        }

        // O alvo é a fatia como está desenhada: girada na troca de aba e, a em hover, maior.
        var (centerX, centerY) = DiscCenter();
        var growth = _motion == MotionStyle.Slide && _hoverSlot is { } hovered ? Hover[hovered].Value : 0;
        var slot = RadialMenu.SlotAt((x - centerX) / _scale, (y - centerY) / _scale, Rotation(), _hoverSlot, growth);
        return slot < RadialMenu.Slices(Carousel.Target, file.First).Count ? slot : null;
    }

    private static double Rotation() =>
        _motion == MotionStyle.Slide ? -TabRotation * Carousel.DistanceTo(Carousel.Target) : 0;

    private static (int X, int Y) DiscCenter() =>
        (_left + (int)Math.Round(DiscLayout.CenterX * _scale), _top + (int)Math.Round(DiscLayout.CenterY * _scale));

    /// <summary>
    /// Soltou fora de uma fatia. Nada é copiado (SEC-05). Com o menu aberto e um arquivo só, ele fica
    /// para a escolha por clique, que começa quando a espera depois de soltar termina (ADR-025).
    /// </summary>
    public static void OnDropOutside()
    {
        if (Session.State == GestureState.Menu && CanPick)
        {
            return;
        }

        Log.Write(LogEvent.MenuCancelled);
        Hide();
    }

    /// <summary>Estado 2: só a camada invisível, até o OLE confirmar que o arraste é de arquivos.</summary>
    private static void BeginGesture()
    {
        OptionsPanel.Close();
        _awaitingOptions = null;
        _dropped = null;
        _pulseSlot = null;
        KillTimer(Disc, TimerAfterRelease);
        KillTimer(Disc, TimerPulse);
        ExitPicking();
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

    private static void ShowMenu(int x, int y)
    {
        var point = new POINT { X = x, Y = y };
        var monitor = MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);
        uint dpiX, dpiY;
        if (GetDpiForMonitor(monitor, 0, &dpiX, &dpiY) != S_OK)
        {
            dpiX = 96;
        }

        var info = new MONITORINFO { CbSize = (uint)sizeof(MONITORINFO) };
        GetMonitorInfoW(monitor, &info);

        // O disco nasce centrado no cursor; perto da borda, o conjunto desloca para caber inteiro (WF-1, nota 5).
        _scale = dpiX / 96.0;
        var width = (int)Math.Round(DiscLayout.Width * _scale);
        var height = (int)Math.Round(DiscLayout.Height * _scale);
        _left = Math.Max(info.RcWork.Left, Math.Min(x - (int)Math.Round(DiscLayout.CenterX * _scale), info.RcWork.Right - width));
        _top = Math.Max(info.RcWork.Top, Math.Min(y - (int)Math.Round(DiscLayout.CenterY * _scale), info.RcWork.Bottom - height));

        Carousel.Reset();
        _hoverSlot = null;
        foreach (var transition in Hover)
        {
            transition.Snap(0);
        }

        _motion = ResolveMotion();

        // O vidro copia a tela antes de o menu aparecer, para não se desfocar a si mesmo.
        DiscRenderer.Prepare(_left, _top, width, height, (int)dpiX);
        IsVisible = true;
        Render();
        SetWindowPos(Disc, HWND_TOPMOST, _left, _top, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        Log.Write(LogEvent.MenuOpened);
    }

    public static MotionStyle ResolveMotion()
    {
        int animations = 1;
        SystemParametersInfoW(SPI_GETCLIENTAREAANIMATION, 0, &animations, 0);

        // Até existirem as Configurações, a opção vem de PIZZA_ANIMATIONS (always, windows ou never).
        var setting = MotionPolicy.Parse(Environment.GetEnvironmentVariable("PIZZA_ANIMATIONS"));
        return MotionPolicy.Resolve(setting, animations != 0);
    }

    private static void Hide()
    {
        Session.End();
        KillTimer(Disc, TimerLeave);
        KillTimer(Disc, TimerAfterRelease);
        KillTimer(Disc, TimerWatchdog);
        KillTimer(Disc, TimerPulse);
        KillTimer(Disc, TimerPicking);
        ExitPicking();
        ShowWindow(_catcher, SW_HIDE);
        HideWindow();
        _files = null;
        _dropped = null;
        _pulseSlot = null;
    }

    /// <summary>Some com o menu e descarta a cópia da tela usada no vidro (ADR-023).</summary>
    private static void HideWindow()
    {
        KillTimer(Disc, TimerAnimation);
        _lastFrame = 0;
        ShowWindow(Disc, SW_HIDE);
        IsVisible = false;
        _hoverSlot = null;
        DiscRenderer.Release();
    }

    private static void SetHover(int? slot)
    {
        if (_hoverSlot is { } previous)
        {
            Move(Hover[previous], 0);
        }

        if (slot is { } next)
        {
            Move(Hover[next], 1);
        }

        _hoverSlot = slot;
        StartAnimation();
    }

    private static void Move(CardTransition transition, double target)
    {
        if (_motion == MotionStyle.Instant)
        {
            transition.Snap(target);
        }
        else
        {
            transition.Retarget(target);
        }
    }

    private static void Render()
    {
        if (!IsVisible)
        {
            return;
        }

        var fileName = _files is { } files ? Path.GetFileName(files.First) : null;
        var slices = _files is { } dragged ? RadialMenu.Slices(Carousel.Target, dragged.First) : [];
        var rotation = Rotation();
        var frame = new DiscFrame(
            _scale,
            fileName,
            _files?.Count ?? 0,
            slices,
            Hover.Select(transition => transition.Value).ToArray(),
            _motion == MotionStyle.Instant ? Carousel.Target : Carousel.Position,
            Carousel.Target,
            rotation,
            _motion,
            _pulseSlot,
            _picking);
        DiscRenderer.Show(Disc, _left, _top, frame);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case WmGestureStarted:
                // Clique numa fatia com o Ctrl ainda apertado: o pulso já confirmou, não é um gesto novo.
                if (_dropped is null)
                {
                    BeginGesture();
                }

                return 0;

            case WmGestureReleased:
                OnButtonReleased();
                return 0;

            case WmWheel:
                if (IsVisible && _pulseSlot is null && Carousel.OnWheel((int)lParam))
                {
                    SetHover(null);
                    if (_motion != MotionStyle.Slide)
                    {
                        Carousel.Snap();
                    }

                    StartAnimation();
                }

                KeepPicking();
                return 0;

            case WM_TIMER:
                OnTimer(wParam);
                return 0;

            case WM_MOUSEMOVE when _picking:
                var (moveX, moveY) = ScreenPoint(hwnd, lParam);
                var hovered = hwnd == Disc ? TargetSlot(moveX, moveY) : null;
                if (hovered != _hoverSlot)
                {
                    SetHover(hovered);
                }

                KeepPicking();
                return 0;

            case WM_LBUTTONDOWN or WM_RBUTTONDOWN when _picking && hwnd == Disc:
                // Os dois botões escolhem a fatia; o painel abre quando o formato tem opções.
                var (clickX, clickY) = ScreenPoint(hwnd, lParam);
                PickAt(clickX, clickY);
                return 0;

            case WM_LBUTTONDOWN when _picking:
                // Clique fora: cancela quando o botão subir, para a soltura também ficar com a camada e não
                // abrir nada na janela de baixo.
                _cancelOnRelease = true;
                SetCapture(hwnd);
                return 0;

            case WM_RBUTTONDOWN or WM_RBUTTONUP when _picking:
                // Botão direito fora das fatias não faz nada, nem chega à janela de baixo.
                return 0;

            case WM_LBUTTONUP when _cancelOnRelease:
                _cancelOnRelease = false;
                ReleaseCapture();
                CancelPicking();
                return 0;

            case WM_KEYDOWN when _picking && (int)wParam == VK_ESCAPE:
                CancelPicking();
                return 0;

            case WM_KEYDOWN when _picking:
                KeepPicking();
                return 0;

            case WM_ACTIVATE when (_picking || _cancelOnRelease) && hwnd == _catcher && (wParam & 0xFFFF) == 0:
                // Outra janela tomou o foco (Alt+Tab, tecla Windows): a escolha termina.
                CancelPicking();
                return 0;

            case WM_ERASEBKGND:
                return 1;

            case WM_PAINT:
                PaintCatcher(hwnd);
                return 0;

            default:
                return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }

    private static void StartAnimation()
    {
        Render();

        // Já animando: só o destino muda, e o movimento em curso continua sem tranco.
        if (_lastFrame == 0 && IsAnimating())
        {
            _lastFrame = Stopwatch.GetTimestamp();
            SetTimer(Disc, TimerAnimation, AnimationMs, 0);
        }
    }

    private static bool IsAnimating() => Carousel.IsAnimating || Hover.Any(transition => !transition.IsDone);

    private static void AnimationFrame()
    {
        // O tempo real decorrido, e não o intervalo do timer, mantém a velocidade constante.
        var now = Stopwatch.GetTimestamp();
        var elapsedMs = Stopwatch.GetElapsedTime(_lastFrame, now).TotalMilliseconds;
        _lastFrame = now;

        Carousel.Advance(elapsedMs);
        foreach (var transition in Hover)
        {
            transition.Advance(elapsedMs);
        }

        Render();
        if (!IsAnimating())
        {
            KillTimer(Disc, TimerAnimation);
            _lastFrame = 0;
        }
    }

    private static void OnButtonReleased()
    {
        KillTimer(Disc, TimerWatchdog);
        if (Session.OnButtonReleased() == SessionAction.WaitForDrop)
        {
            SetTimer(Disc, TimerAfterRelease, AfterReleaseMs, 0);
        }
    }

    private static void OnTimer(nuint id)
    {
        if (id == TimerAnimation)
        {
            AnimationFrame();
            return;
        }

        if (id == TimerPulse)
        {
            FinishDrop();
            return;
        }

        if (id == TimerWatchdog)
        {
            // A camada cobre a tela inteira: se a soltura do botão se perder, ela não pode ficar presa.
            if (!IsPrimaryButtonDown())
            {
                OnButtonReleased();
            }

            return;
        }

        if (id == TimerLeave)
        {
            KillTimer(Disc, TimerLeave);
            if (Session.OnLeaveTimeout() == SessionAction.Close)
            {
                Log.Write(LogEvent.MenuCancelled);
                Hide();
            }

            return;
        }

        if (id == TimerAfterRelease)
        {
            KillTimer(Disc, TimerAfterRelease);

            // ADR-025: soltou fora de uma fatia, com um arquivo só: o menu fica para a escolha por clique.
            if (Session.OnReleaseSettled(CanPick) == SessionAction.Pick)
            {
                EnterPicking();
                return;
            }

            // Só houve cancelamento se o menu chegou a abrir.
            if (IsVisible)
            {
                Log.Write(LogEvent.MenuCancelled);
            }
        }

        if (id == TimerPicking)
        {
            // Ninguém escolheu: o menu e a cópia da tela não ficam abertos para sempre (ADR-023).
            CancelPicking();
            return;
        }

        Hide();
    }

    /// <summary>
    /// A camada que cobre a tela recebe o foco: Esc chega por mensagem da própria janela, sem hook de
    /// teclado (SEC-01), e um clique fora do menu cai nela e fecha, sem chegar à janela de baixo.
    /// </summary>
    private static void EnterPicking()
    {
        _picking = true;
        SetWindowLongPtrW(_catcher, GWL_EXSTYLE, GetWindowLongPtrW(_catcher, GWL_EXSTYLE) & ~(nint)WS_EX_NOACTIVATE);
        OptionsPanel.TakeFocus(_catcher);

        // Ganhar o foco traz a camada para a frente; o disco precisa voltar para cima dela, senão todo
        // clique no menu cai na camada e conta como clique fora.
        SetWindowPos(Disc, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        KeepPicking();
        Render();
    }

    /// <summary>Qualquer interação na escolha (mouse, roda, teclado) reinicia o tempo até fechar.</summary>
    private static void KeepPicking()
    {
        if (_picking)
        {
            SetTimer(Disc, TimerPicking, PickingTimeoutMs, 0);
        }
    }

    private static void ExitPicking()
    {
        KillTimer(Disc, TimerPicking);
        if (_cancelOnRelease)
        {
            _cancelOnRelease = false;
            ReleaseCapture();
        }

        if (_picking)
        {
            _picking = false;
            SetWindowLongPtrW(_catcher, GWL_EXSTYLE, GetWindowLongPtrW(_catcher, GWL_EXSTYLE) | (nint)WS_EX_NOACTIVATE);
        }
    }

    private static void CancelPicking()
    {
        Log.Write(LogEvent.MenuCancelled);
        Hide();
    }

    /// <summary>Clique no menu durante a escolha: a fatia sob o cursor converte, como se o arquivo tivesse caído nela.</summary>
    private static void PickAt(int x, int y)
    {
        if (_files is { Count: 1 } file && TargetSlot(x, y) is { } slot)
        {
            StartPulse(file.First, slot);
        }
    }

    private static (int X, int Y) ScreenPoint(nint hwnd, nint lParam)
    {
        var x = (short)(lParam & 0xFFFF);
        var y = (short)((lParam >> 16) & 0xFFFF);
        return hwnd == Disc ? (_left + x, _top + y) : (x + GetSystemMetrics(SM_XVIRTUALSCREEN), y + GetSystemMetrics(SM_YVIRTUALSCREEN));
    }

    private static bool IsPrimaryButtonDown()
    {
        // GetAsyncKeyState lê o botão físico; com os botões trocados no Windows, o principal é o direito.
        var button = GetSystemMetrics(SM_SWAPBUTTON) != 0 ? VK_RBUTTON : VK_LBUTTON;
        return (GetAsyncKeyState(button) & 0x8000) != 0;
    }

    /// <summary>A camada é quase invisível: só precisa de algo pintado para o Windows entregar o mouse a ela.</summary>
    private static void PaintCatcher(nint hwnd)
    {
        PAINTSTRUCT ps;
        var hdc = BeginPaint(hwnd, &ps);
        var brush = CreateSolidBrush(0);
        var area = ps.RcPaint;
        FillRect(hdc, &area, brush);
        DeleteObject(brush);
        EndPaint(hwnd, &ps);
    }
}
