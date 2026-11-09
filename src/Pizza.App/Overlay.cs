using System.Diagnostics;
using Pizza.App.Interop;
using Pizza.Core.Diagnostics;
using Pizza.Core.Gesture;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// Overlay da prova de conceito (etapa 1a): um disco simples em GDI, sem vidro. O visual final vem na Fase 4.
/// São duas janelas: o disco, que aceita o drop, e uma camada quase invisível sobre todas as telas,
/// que recusa o drop para que soltar fora do menu não vire uma cópia no Explorer.
/// </summary>
internal static unsafe class Overlay
{
    public const uint WmGestureStarted = WM_APP + 1;
    public const uint WmGestureReleased = WM_APP + 2;
    public const uint WmWheel = WM_APP + 3;
    public const int HotkeyQuit = 1;

    private const string ClassName = "PizzaOverlay";
    private const int DiscSize = 380;
    private const nuint TimerAfterRelease = 1;
    private const nuint TimerHideResult = 2;
    private const nuint TimerWatchdog = 3;
    private const nuint TimerAnimation = 4;
    private const nuint TimerLeave = 5;
    private const uint LeaveGraceMs = 80;
    private const uint WatchdogMs = 250;
    private const uint AnimationMs = 10;
    private const uint AfterReleaseMs = 400;
    private const uint ResultMs = 3000;

    // A camada precisa de alfa maior que zero, senão o Windows deixa o mouse atravessar.
    private const byte CatcherAlpha = 1;
    private const byte DiscAlpha = 235;

    private static readonly uint DiscColor = Rgb(26, 33, 64);
    private static readonly DropTarget DiscTarget = new(acceptsDrop: true);
    private static readonly DropTarget CatcherTarget = new(acceptsDrop: false);

    private static nint _catcher;
    private static readonly TabCarousel Carousel = new(MenuTabs.Names.Count);
    private static readonly GestureSession Session = new();
    private static long _lastFrame;
    private static MotionStyle _motion = MotionStyle.Slide;
    private static int _size = DiscSize;
    private static DraggedFiles? _files;
    private static DraggedFiles? _result;

    /// <summary>Janela do disco; também recebe as mensagens enviadas pelos hooks.</summary>
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
        Disc = CreateWindowExW(exStyle, ClassName, "Pizza", WS_POPUP, 0, 0, DiscSize, DiscSize, 0, 0, instance, 0);
        SetLayeredWindowAttributes(_catcher, 0, CatcherAlpha, LWA_ALPHA);
        SetLayeredWindowAttributes(Disc, 0, DiscAlpha, LWA_ALPHA);

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
        var action = Session.OnDragEnter(files is not null);
        if (action == SessionAction.Close)
        {
            // O gesto não era de arquivo (texto, por exemplo): sai da frente e deixa o arraste seguir.
            Log.Write(LogEvent.NotAFile);
            Hide();
            return;
        }

        if (action == SessionAction.ShowMenu)
        {
            ShowMenu(x, y);
        }

        if (files is { } dragged && _files?.First != dragged.First)
        {
            _files = files;
            Invalidate();
        }
    }

    public static void OnDragLeave()
    {
        if (Session.OnDragLeave() == SessionAction.WaitForReentry)
        {
            SetTimer(Disc, TimerLeave, LeaveGraceMs, 0);
        }
    }

    public static void OnDrop(DraggedFiles? files)
    {
        if (files is null)
        {
            Hide();
            return;
        }

        Log.Write(LogEvent.FileDropped);
        Session.End();
        KillTimer(Disc, TimerAfterRelease);
        KillTimer(Disc, TimerWatchdog);
        KillTimer(Disc, TimerLeave);
        _result = files;
        ShowWindow(_catcher, SW_HIDE);
        Invalidate();
        SetTimer(Disc, TimerHideResult, ResultMs, 0);
    }

    public static void OnDropOutside()
    {
        Log.Write(LogEvent.MenuCancelled);
        Hide();
    }

    /// <summary>Estado 2: só a camada invisível, até o OLE confirmar que o arraste é de arquivos.</summary>
    private static void BeginGesture()
    {
        KillTimer(Disc, TimerAfterRelease);
        KillTimer(Disc, TimerHideResult);
        ShowWindow(Disc, SW_HIDE);
        IsVisible = false;
        _files = null;
        _result = null;
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

        // Perto da borda, desloca para o disco caber inteiro na tela (WF-1, nota 5).
        _size = DiscSize * (int)dpiX / 96;
        var left = Math.Max(info.RcWork.Left, Math.Min(x - (_size / 2), info.RcWork.Right - _size));
        var top = Math.Max(info.RcWork.Top, Math.Min(y - (_size / 2), info.RcWork.Bottom - _size));

        Carousel.Reset();
        int animations = 1;
        SystemParametersInfoW(SPI_GETCLIENTAREAANIMATION, 0, &animations, 0);

        // Até existirem as Configurações, a opção vem de PIZZA_ANIMATIONS (always, windows ou never).
        var setting = MotionPolicy.Parse(Environment.GetEnvironmentVariable("PIZZA_ANIMATIONS"));
        _motion = MotionPolicy.Resolve(setting, animations != 0);

        // A posição e o tamanho são definidos antes da região, que é relativa à janela.
        SetWindowPos(Disc, HWND_TOPMOST, left, top, _size, _size, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        SetWindowRgn(Disc, CreateEllipticRgn(0, 0, _size + 1, _size + 1), 1);

        IsVisible = true;
        Invalidate();
        Log.Write(LogEvent.MenuOpened);
    }

    private static void Hide()
    {
        Session.End();
        KillTimer(Disc, TimerLeave);
        KillTimer(Disc, TimerAfterRelease);
        KillTimer(Disc, TimerHideResult);
        KillTimer(Disc, TimerWatchdog);
        KillTimer(Disc, TimerAnimation);
        _lastFrame = 0;
        ShowWindow(_catcher, SW_HIDE);
        ShowWindow(Disc, SW_HIDE);
        IsVisible = false;
        _files = null;
        _result = null;
    }

    private static void Invalidate() => InvalidateRect(Disc, null, 0);

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

            case WmWheel:
                if (IsVisible && _result is null && Carousel.OnWheel((int)lParam))
                {
                    StartAnimation();
                }

                return 0;

            case WM_TIMER:
                OnTimer(wParam);
                return 0;

            case WM_HOTKEY:
                if ((int)wParam == HotkeyQuit)
                {
                    PostQuitMessage(0);
                }

                return 0;

            case WM_ERASEBKGND:
                return 1;

            case WM_PAINT:
                Paint(hwnd);
                return 0;

            default:
                return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }

    private static void StartAnimation()
    {
        if (_motion == MotionStyle.Instant)
        {
            Carousel.Snap();
            Invalidate();
            return;
        }

        // Já animando: só o destino muda, e o deslize em curso continua sem tranco.
        if (_lastFrame == 0)
        {
            _lastFrame = Stopwatch.GetTimestamp();
            SetTimer(Disc, TimerAnimation, AnimationMs, 0);
        }
    }

    private static void AnimationFrame()
    {
        // O tempo real decorrido, e não o intervalo do timer, mantém a velocidade constante.
        var now = Stopwatch.GetTimestamp();
        var elapsedMs = Stopwatch.GetElapsedTime(_lastFrame, now).TotalMilliseconds;
        _lastFrame = now;

        Carousel.Advance(elapsedMs);
        Invalidate();
        if (!Carousel.IsAnimating)
        {
            KillTimer(Disc, TimerAnimation);
            _lastFrame = 0;
        }
    }

    private static void OnButtonReleased()
    {
        if (_result is not null)
        {
            return;
        }

        KillTimer(Disc, TimerWatchdog);
        if (Session.State == GestureState.WaitingForFile)
        {
            // Nenhum arraste OLE chegou (Ctrl + arraste de texto ou de células, por exemplo).
            Hide();
        }
        else if (Session.State == GestureState.Menu)
        {
            // O hook vê o botão subir antes de o Explorer chamar Drop: espera um pouco antes de fechar.
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

        if (id == TimerAfterRelease && _result is null)
        {
            Log.Write(LogEvent.MenuCancelled);
        }

        Hide();
    }

    private static bool IsPrimaryButtonDown()
    {
        // GetAsyncKeyState lê o botão físico; com os botões trocados no Windows, o principal é o direito.
        var button = GetSystemMetrics(SM_SWAPBUTTON) != 0 ? VK_RBUTTON : VK_LBUTTON;
        return (GetAsyncKeyState(button) & 0x8000) != 0;
    }

    private static void Paint(nint hwnd)
    {
        PAINTSTRUCT ps;
        var hdc = BeginPaint(hwnd, &ps);
        if (hwnd == Disc)
        {
            // Desenha fora da tela e copia de uma vez: é o que evita o piscar durante o deslize.
            var buffer = CreateCompatibleDC(hdc);
            var bitmap = CreateCompatibleBitmap(hdc, _size, _size);
            var oldBitmap = SelectObject(buffer, bitmap);
            PaintDisc(buffer);
            BitBlt(hdc, 0, 0, _size, _size, buffer, 0, 0, SRCCOPY);
            SelectObject(buffer, oldBitmap);
            DeleteObject(bitmap);
            DeleteDC(buffer);
        }
        else
        {
            var brush = CreateSolidBrush(0);
            var area = ps.RcPaint;
            FillRect(hdc, &area, brush);
            DeleteObject(brush);
        }

        EndPaint(hwnd, &ps);
    }

    private static void PaintDisc(nint hdc)
    {
        var whole = new RECT { Right = _size, Bottom = _size };
        var background = CreateSolidBrush(DiscColor);
        FillRect(hdc, &whole, background);
        DeleteObject(background);

        var margin = Scale(3);
        var ring = CreatePen(PS_SOLID, Scale(4), _result is null ? Rgb(47, 107, 255) : Rgb(64, 200, 130));
        var oldPen = SelectObject(hdc, ring);
        var oldBrush = SelectObject(hdc, GetStockObject(NULL_BRUSH));
        Ellipse(hdc, margin, margin, _size - margin, _size - margin);
        SelectObject(hdc, oldBrush);
        SelectObject(hdc, oldPen);
        DeleteObject(ring);

        SetBkMode(hdc, TRANSPARENT);
        if (_result is { } result)
        {
            DrawLine(hdc, "Recebido", 110, 24, FW_BOLD, Rgb(120, 225, 165));
            DrawBlock(hdc, Describe(result, result.First), 150, 230, 14, FW_NORMAL, Rgb(255, 255, 255));
            return;
        }

        DrawTabs(hdc);
        DrawTabDots(hdc);
        DrawLine(hdc, _files is { } files ? Describe(files, Path.GetFileName(files.First)) : "Solte aqui", 178, 17, FW_SEMIBOLD, Rgb(255, 255, 255));
        DrawLine(hdc, "Role para trocar a aba", 262, 13, FW_NORMAL, Rgb(170, 182, 214));
        DrawLine(hdc, "Solte o Ctrl e aperte Esc para cancelar", 284, 13, FW_NORMAL, Rgb(170, 182, 214));
    }

    /// <summary>Nomes das abas numa faixa que desliza: a central acesa, as vizinhas apagando nas pontas.</summary>
    private static void DrawTabs(nint hdc)
    {
        const int spacing = 128;
        const int halfWidth = 90;
        var center = _size / 2;

        for (var tab = 0; tab < MenuTabs.Names.Count; tab++)
        {
            var distance = Carousel.DistanceTo(tab);
            var absolute = Math.Abs(distance);
            if (absolute >= 1.5)
            {
                continue;
            }

            if (_motion == MotionStyle.Fade && absolute >= 1)
            {
                continue;
            }

            // Em "menos movimento" a aba não anda: só troca de opacidade no mesmo lugar.
            var brightness = _motion == MotionStyle.Fade ? 1 - absolute
                : absolute < 1 ? 1 - (0.75 * absolute) : 0.25 * (1.5 - absolute) / 0.5;
            var x = _motion == MotionStyle.Fade ? center : center + (int)Math.Round(distance * Scale(spacing));
            var rect = new RECT { Left = x - Scale(halfWidth), Top = Scale(84), Right = x + Scale(halfWidth), Bottom = Scale(128) };
            Draw(hdc, MenuTabs.Names[tab], &rect, 26, FW_BOLD, Mix(DiscColor, Rgb(255, 255, 255), brightness), DT_CENTER | DT_SINGLELINE | DT_VCENTER);
        }
    }

    /// <summary>Um ponto por aba; o brilho acompanha a posição do deslize.</summary>
    private static void DrawTabDots(nint hdc)
    {
        const int gap = 18;
        const int radius = 4;
        var count = MenuTabs.Names.Count;
        var first = (_size / 2) - (Scale(gap) * (count - 1) / 2);
        var y = Scale(136);
        var oldPen = SelectObject(hdc, GetStockObject(NULL_PEN));

        for (var tab = 0; tab < count; tab++)
        {
            var lit = Math.Max(0, 1 - Math.Abs(Carousel.DistanceTo(tab)));
            var brush = CreateSolidBrush(Mix(Rgb(84, 96, 136), Rgb(47, 107, 255), lit));
            var oldBrush = SelectObject(hdc, brush);
            var x = first + (tab * Scale(gap));
            Ellipse(hdc, x - Scale(radius), y - Scale(radius), x + Scale(radius), y + Scale(radius));
            SelectObject(hdc, oldBrush);
            DeleteObject(brush);
        }

        SelectObject(hdc, oldPen);
    }

    private static uint Mix(uint from, uint to, double amount)
    {
        static uint Channel(uint a, uint b, double t) => (uint)Math.Round(a + ((double)b - a) * t);

        var r = Channel(from & 0xFF, to & 0xFF, amount);
        var g = Channel((from >> 8) & 0xFF, (to >> 8) & 0xFF, amount);
        var b = Channel((from >> 16) & 0xFF, (to >> 16) & 0xFF, amount);
        return r | (g << 8) | (b << 16);
    }

    private static string Describe(DraggedFiles files, string text) =>
        files.Count > 1 ? $"{text} (+{files.Count - 1})" : text;

    private static void DrawLine(nint hdc, string text, int top, int fontSize, int weight, uint color)
    {
        var rect = new RECT { Left = Scale(46), Top = Scale(top), Right = _size - Scale(46), Bottom = Scale(top + (fontSize * 2)) };
        Draw(hdc, text, &rect, fontSize, weight, color, DT_CENTER | DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
    }

    private static void DrawBlock(nint hdc, string text, int top, int bottom, int fontSize, int weight, uint color)
    {
        var rect = new RECT { Left = Scale(46), Top = Scale(top), Right = _size - Scale(46), Bottom = Scale(bottom) };
        Draw(hdc, text, &rect, fontSize, weight, color, DT_CENTER | DT_WORDBREAK);
    }

    private static void Draw(nint hdc, string text, RECT* rect, int fontSize, int weight, uint color, uint format)
    {
        var font = CreateFontW(-Scale(fontSize), 0, 0, 0, weight, 0, 0, 0, 1, 0, 0, CLEARTYPE_QUALITY, 0, "Segoe UI");
        var old = SelectObject(hdc, font);
        SetTextColor(hdc, color);
        DrawTextW(hdc, text, -1, rect, format);
        SelectObject(hdc, old);
        DeleteObject(font);
    }

    private static int Scale(int value) => value * _size / DiscSize;
}
