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
/// quase invisível sobre todas as telas, que prepara a escolha por clique ao soltar fora das fatias (ADR-025).
/// </summary>
internal static unsafe partial class Overlay
{
    public const uint WmGestureStarted = WM_APP + 1;
    public const uint WmGestureReleased = WM_APP + 2;
    public const uint WmWheel = WM_APP + 3;
    private const uint WmChoiceReady = WM_APP + 4;

    private const string ClassName = "PizzaOverlay";
    private const nuint TimerAfterRelease = 1;
    private const nuint TimerWatchdog = 3;
    private const nuint TimerAnimation = 4;
    private const nuint TimerLeave = 5;
    private const nuint TimerPulse = 6;
    private const nuint TimerChoice = 7;
    private const uint ChoiceIdleMs = 10_000; // ADR-025: 10 s sem nenhuma interação, a escolha fecha.
    private static long _choiceDeadline;
    private static long _dropDeadline;
    private static long _gestureStarted; // NFR-03: do início do gesto até o menu na tela.
    private static (int Width, int Height)? _prepared; // Vidro já preparado no início do gesto.
    private const uint LeaveGraceMs = 80;
    private const uint WatchdogMs = 250;
    private const uint AnimationMs = 10;
    private const uint AfterReleaseMs = 400;
    private const uint PulseMs = 120; // WF-2: pulso ao soltar.
    private const double HoverMs = 180; // WF-2: 180 ms, curva "ease".
    private const double TabRotation = 24; // FR-04: as fatias giram ao trocar de aba; 24° deixa o giro visível.

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
    private static IReadOnlyList<MenuPage> _pages = RadialMenu.Pages("a.jpg");
    private static bool _clickArmed;
    private static int _left;
    private static int _top;
    private static int? _hoverSlot;
    private static int? _pulseSlot;

    /// <summary>ADR-036: fatia apagada que a pessoa escolheu, na página em que estava; mostra a aba que serve.</summary>
    private static MenuPage? _hintPage;
    private static int _hintSlot;
    private static DraggedFiles? _files;
    private static (IReadOnlyList<string> Paths, ConverterInfo Converter, IReadOnlyDictionary<string, string>? Options)? _dropped; // Options null: abrir o painel.
    private static (IReadOnlyList<string> Paths, ConverterInfo Converter)? _awaitingOptions;

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

    /// <summary>O disco nasce centrado no cursor; perto da borda, o conjunto desloca para caber inteiro (WF-1, nota 5).</summary>
    private static (int Width, int Height, int Dpi) Place(int x, int y)
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

        _scale = dpiX / 96.0;
        var width = (int)Math.Round(DiscLayout.Width * _scale);
        var height = (int)Math.Round(DiscLayout.Height * _scale);
        _left = Math.Max(info.RcWork.Left, Math.Min(x - (int)Math.Round(DiscLayout.CenterX * _scale), info.RcWork.Right - width));
        _top = Math.Max(info.RcWork.Top, Math.Min(y - (int)Math.Round(DiscLayout.CenterY * _scale), info.RcWork.Bottom - height));
        return (width, height, (int)dpiX);
    }

    /// <summary>
    /// NFR-03: copia e desfoca a tela já no início do gesto, enquanto o Windows ainda não confirmou o arraste de
    /// arquivos. Se não for arquivo, o fechamento zera a cópia (ADR-023), como antes.
    /// </summary>
    private static void PrepareAtCursor()
    {
        POINT cursor;
        if (GetCursorPos(&cursor) == 0)
        {
            return;
        }

        var (width, height, dpi) = Place(cursor.X, cursor.Y);
        DiscRenderer.Prepare(_left, _top, width, height, dpi);
        _prepared = (width, height);
    }

    private static void ShowMenu(int x, int y)
    {
        // A cópia feita no início do gesto vale para a primeira abertura; o menu fica onde ela foi feita.
        var (width, height) = _prepared ?? default;
        if (_prepared is null)
        {
            (width, height, var dpi) = Place(x, y);
            DiscRenderer.Prepare(_left, _top, width, height, dpi);
        }

        _prepared = null;
        _pages = RadialMenu.Pages(_files!.Value.First);
        Carousel.Reset(_pages.Count, RadialMenu.FirstPage(_files!.Value.First));
        _hoverSlot = null;
        _levelHover = null;
        Strip.Clear();
        SliceGlide.Snap(0);
        LevelGlide.Snap(0);
        foreach (var transition in Hover)
        {
            transition.Snap(0);
        }

        _motion = ResolveMotion();

        // O vidro copiou a tela antes de o menu aparecer, para não se desfocar a si mesmo.
        IsVisible = true;
        Render();
        SetWindowPos(Disc, HWND_TOPMOST, _left, _top, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        int? elapsed = _gestureStarted == 0 ? null : (int)System.Diagnostics.Stopwatch.GetElapsedTime(_gestureStarted).TotalMilliseconds;
        _gestureStarted = 0;
        Log.Write(LogEvent.MenuOpened, elapsed);
        MenuOpened?.Invoke(_files!.Value.First);
    }

    /// <summary>ADR-034: o menu abriu sobre este arquivo; quem precisar se prepara (aquecimento do motor de documentos).</summary>
    public static Action<string>? MenuOpened { get; set; }

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
        KillTimer(Disc, TimerChoice);
        KillTimer(Disc, TimerStrip);
        ShowWindow(_catcher, SW_HIDE);
        HideWindow();
        Strip.Clear();
        _levelHover = null;
        _files = null;
        _hintPage = null;
        _dropped = null;
        _pulseSlot = null;
        _clickArmed = false;
    }

    /// <summary>Some com o menu e descarta a cópia da tela usada no vidro (ADR-023).</summary>
    private static void HideWindow()
    {
        KillTimer(Disc, TimerAnimation);
        _lastFrame = 0;
        ShowWindow(Disc, SW_HIDE);
        IsVisible = false;
        _hoverSlot = null;
        _prepared = null;
        DiscRenderer.Release();
    }

    private static void Render()
    {
        if (!IsVisible)
        {
            return;
        }

        var fileName = _files is { } files ? Path.GetFileName(files.First) : null;
        var page = _pages[Carousel.Target];
        var slices = CurrentSlices();
        var rotation = Rotation();
        var levels = Strip.Primary is null ? null : Strip.Levels.Select((choice, level) => (choice.Label, Strip.Detail(level))).ToArray();

        // FR-30 com a emenda da ADR-033: fatias que não valem para o que foi arrastado ficam esmaecidas; com vários
        // PDFs e o Juntar na faixa, ela mostra a ordem em que vão ficar (prancha 6A-1).
        var unavailable = _files is { } dragged ? slices.Select(slice => !slice.Accepts(dragged.Paths)).ToArray() : null;
        var merging = _files is { Count: > 1 } many && Usable(many);
        var order = merging && Strip.Converter is { Merges: true } ? MergeOrder.Chips(_files!.Value.Paths) : null;
        var frame = new DiscFrame(
            _scale,
            fileName,
            _files?.Count ?? 0,
            slices,
            Hover.Select(transition => transition.Value).ToArray(),
            TabPosition(_motion == MotionStyle.Instant ? Carousel.Target : Carousel.Position),
            page.Tab,
            rotation,
            _motion,
            _pulseSlot,
            page.Turn,
            _pages.Count(candidate => candidate.Tab == page.Tab),
            Session.State == GestureState.Choosing,
            order is null ? Strip.Title : "Juntar nesta ordem",
            levels,
            Strip.Selected,
            _levelHover,
            SliceGlide.Position,
            SliceGlide.Swell,
            LevelGlide.Position,
            LevelGlide.Swell,
            unavailable,
            order,
            merging,
            _hintPage == page ? _hintSlot : null,
            _files is { } hinted ? RadialMenu.Hint(hinted.First) : null);
        DiscRenderer.Show(Disc, _left, _top, frame);
    }

    private static double TabPosition(double position)
    {
        var wrapped = ((position % _pages.Count) + _pages.Count) % _pages.Count;
        var from = (int)Math.Floor(wrapped);
        var to = (from + 1) % _pages.Count;
        var nextTab = _pages[to].Tab;
        if (nextTab < _pages[from].Tab)
        {
            nextTab += MenuTabs.Names.Count;
        }

        return _pages[from].Tab + ((nextTab - _pages[from].Tab) * (wrapped - from));
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case WmGestureStarted:
                BeginGesture();
                return 0;

            case WmGestureReleased:
                OnButtonReleased();
                return 0;

            case WmChoiceReady:
                PrepareChoice();
                return 0;

            case WM_MOUSEMOVE when Session.State == GestureState.Choosing:
                OnDragOver(_left + (short)(lParam & 0xFFFF), _top + (short)((lParam >> 16) & 0xFFFF));
                KeepChoosing();
                return 0;

            case WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_LBUTTONUP or WM_RBUTTONUP when Session.State == GestureState.Choosing:
                // O botão direito numa fatia abre as opções do formato; fora das fatias, nenhum dos dois faz nada.
                OnChoiceButton(msg, _left + (short)(lParam & 0xFFFF), _top + (short)((lParam >> 16) & 0xFFFF));
                return 0;

            case WM_KEYDOWN when Session.State == GestureState.Choosing:
                KeepChoosing();
                if ((int)wParam == VK_ESCAPE)
                {
                    OnDropOutside();
                }
                else if ((int)wParam is VK_RETURN or VK_SPACE && _hoverSlot is { } focused && _files is { } selectedFile)
                {
                    Select(selectedFile, focused);
                }
                else if ((int)wParam is VK_TAB or VK_RIGHT or VK_DOWN or VK_LEFT or VK_UP && _files is { } activeFile)
                {
                    var page = _pages[Carousel.Target];
                    var count = RadialMenu.Slices(page.Tab, activeFile.First, page.Turn).Count;
                    if (count > 0)
                    {
                        var backwards = (int)wParam is VK_LEFT or VK_UP || ((int)wParam == VK_TAB && GetKeyState(VK_SHIFT) < 0);
                        SetHover(((_hoverSlot ?? (backwards ? 0 : -1)) + (backwards ? -1 : 1) + count) % count);
                    }
                }

                return 0;

            case WM_ACTIVATE when Session.State == GestureState.Choosing && (wParam & 0xFFFF) == 0:
            case WM_CLOSE:
                OnDropOutside();
                return 0;

            case WmWheel:
                // O hook só manda o giro; a posição vem do cursor, sem dado novo do hook (SEC-01).
                POINT cursor;
                GetCursorPos(&cursor);
                OnWheel((int)lParam, cursor.X, cursor.Y);
                return 0;

            case WM_TIMER:
                OnTimer(wParam);
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

        if (id == TimerStrip)
        {
            OnStripTimer();
            return;
        }

        if (id == TimerChoice)
        {
            // Ninguém escolheu: o menu e a cópia da tela não ficam abertos para sempre (ADR-023).
            KillTimer(Disc, TimerChoice);
            if (Session.State == GestureState.Choosing)
            {
                var remaining = _choiceDeadline - Environment.TickCount64;
                if (remaining <= 0)
                {
                    OnDropOutside();
                }
                else if (Disc != 0)
                {
                    SetTimer(Disc, TimerChoice, (uint)remaining, 0);
                }
            }

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
            var remaining = _dropDeadline - Environment.TickCount64;
            if (remaining > 0 && Session.State is GestureState.Menu or GestureState.WaitingForFile)
            {
                if (Disc != 0) SetTimer(Disc, TimerAfterRelease, (uint)remaining, 0);
            }
            else if (Session.OnDropTimeout() == SessionAction.Close)
            {
                Log.Write(LogEvent.MenuCancelled);
                Hide();
            }
        }
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
