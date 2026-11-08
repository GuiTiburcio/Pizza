using Pizza.App.Interop;
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
    public const uint WmGestureCancelled = WM_APP + 3;
    public const uint WmWheel = WM_APP + 4;
    public const int HotkeyQuit = 1;

    private const string ClassName = "PizzaOverlay";
    private const int DiscSize = 380;
    private const nuint TimerAfterRelease = 1;
    private const nuint TimerHideResult = 2;
    private const nuint TimerWatchdog = 3;
    private const uint WatchdogMs = 250;
    private const uint AfterReleaseMs = 400;
    private const uint ResultMs = 3000;

    // A camada precisa de alfa maior que zero, senão o Windows deixa o mouse atravessar.
    private const byte CatcherAlpha = 1;
    private const byte DiscAlpha = 235;

    private static readonly DropTarget DiscTarget = new(acceptsDrop: true);
    private static readonly DropTarget CatcherTarget = new(acceptsDrop: false);

    private static nint _catcher;
    private static int _tab;
    private static int _size = DiscSize;
    private static DraggedFiles? _files;
    private static DraggedFiles? _result;

    /// <summary>Janela do disco; também recebe as mensagens enviadas pelos hooks.</summary>
    public static nint Disc { get; private set; }

    public static bool IsVisible { get; private set; }

    public static int GestureX { get; set; }

    public static int GestureY { get; set; }

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

    public static void OnDragEnter(DraggedFiles? files)
    {
        if (files is null)
        {
            // O gesto não era de arquivo (texto, por exemplo): sai da frente e deixa o arraste seguir.
            if (IsVisible && _result is null)
            {
                Log.Write("arraste sem arquivos: overlay fechado");
                Hide();
            }

            return;
        }

        if (_files?.First != files.Value.First)
        {
            _files = files;
            Invalidate();
        }
    }

    public static void OnDrop(DraggedFiles? files)
    {
        if (files is null)
        {
            Hide();
            return;
        }

        Log.Write($"drop na aba {MenuTabs.Names[_tab]}: {files.Value.First} (total: {files.Value.Count})");
        KillTimer(Disc, TimerAfterRelease);
        KillTimer(Disc, TimerWatchdog);
        _result = files;
        ShowWindow(_catcher, SW_HIDE);
        Invalidate();
        SetTimer(Disc, TimerHideResult, ResultMs, 0);
    }

    public static void OnDropOutside()
    {
        Log.Write("drop fora do menu: cancelado");
        Hide();
    }

    private static void Show(int x, int y)
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

        _tab = 0;
        _files = null;
        _result = null;
        KillTimer(Disc, TimerAfterRelease);
        KillTimer(Disc, TimerHideResult);

        // A posição e o tamanho são definidos antes da região, que é relativa à janela.
        SetWindowPos(
            _catcher,
            HWND_TOPMOST,
            GetSystemMetrics(SM_XVIRTUALSCREEN),
            GetSystemMetrics(SM_YVIRTUALSCREEN),
            GetSystemMetrics(SM_CXVIRTUALSCREEN),
            GetSystemMetrics(SM_CYVIRTUALSCREEN),
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
        SetWindowPos(Disc, HWND_TOPMOST, left, top, _size, _size, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        SetWindowRgn(Disc, CreateEllipticRgn(0, 0, _size + 1, _size + 1), 1);

        IsVisible = true;
        Invalidate();
        SetTimer(Disc, TimerWatchdog, WatchdogMs, 0);
        Log.Write($"menu aberto em {x},{y} (disco {_size}px, dpi {dpiX})");
    }

    private static void Hide()
    {
        KillTimer(Disc, TimerAfterRelease);
        KillTimer(Disc, TimerHideResult);
        KillTimer(Disc, TimerWatchdog);
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
                Show(GestureX, GestureY);
                return 0;

            case WmGestureReleased:
                // O hook vê o botão subir antes de o Explorer chamar Drop: espera um pouco antes de fechar.
                if (IsVisible && _result is null)
                {
                    SetTimer(Disc, TimerAfterRelease, AfterReleaseMs, 0);
                }

                return 0;

            case WmGestureCancelled:
                Log.Write("cancelado com Esc");
                Hide();
                return 0;

            case WmWheel:
                if (IsVisible && _result is null)
                {
                    _tab = MenuTabs.Step(_tab, (int)lParam);
                    Log.Write($"aba: {MenuTabs.Names[_tab]}");
                    Invalidate();
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

    private static void OnTimer(nuint id)
    {
        if (id == TimerWatchdog)
        {
            // A camada cobre a tela inteira: se a soltura do botão se perder, ela não pode ficar presa.
            if (_result is null && !IsPrimaryButtonDown())
            {
                KillTimer(Disc, TimerWatchdog);
                SetTimer(Disc, TimerAfterRelease, AfterReleaseMs, 0);
            }

            return;
        }

        if (id == TimerAfterRelease && _result is null)
        {
            Log.Write("botão solto sem drop: overlay fechado");
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
            PaintDisc(hdc);
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
        var background = CreateSolidBrush(Rgb(26, 33, 64));
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

        DrawLine(hdc, MenuTabs.Names[_tab], 80, 28, FW_BOLD, Rgb(255, 255, 255));
        DrawLine(hdc, $"aba {_tab + 1} de {MenuTabs.Names.Count}", 122, 13, FW_NORMAL, Rgb(170, 182, 214));
        DrawLine(hdc, _files is { } files ? Describe(files, Path.GetFileName(files.First)) : "Solte aqui", 178, 17, FW_SEMIBOLD, Rgb(255, 255, 255));
        DrawLine(hdc, "Role para trocar a aba", 262, 13, FW_NORMAL, Rgb(170, 182, 214));
        DrawLine(hdc, "Esc cancela", 284, 13, FW_NORMAL, Rgb(170, 182, 214));
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
