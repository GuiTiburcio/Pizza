using System.Diagnostics;
using Pizza.Core.Conversions;
using Pizza.Core.Gesture;
using Pizza.Core.Notifications;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// Painel "Arquivos..." (FR-44), no canto inferior direito. Fecha ao perder o foco ou com Esc.
/// Vidro translúcido com linhas compactas (ADR-028, prancha 4c-3), desenhado por <see cref="DiscRenderer.ShowFiles"/>.
/// </summary>
internal static unsafe class FilesPanel
{
    private const string ClassName = "PizzaFiles";
    private const nuint TimerFrame = 1;
    private const uint FrameMs = 16;
    private const int Margin = NotificationCardLayout.Margin;
    private const int WaInactive = 0;

    private static readonly FilesPanelState State = new(Notifications.History);
    private static readonly CardTransition Top = new(0);
    private static readonly CardTransition Alpha = new(0);

    private static nint _window;
    private static bool _open;
    private static bool _closing;
    private static long _lastFrame;
    private static int _left;
    private static int _height;

    public static bool IsOpen => _open;

    public static nint Window => _window;

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

        _window = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_LAYERED, ClassName, "Pizza Arquivos", WS_POPUP, 0, 0, 1, 1, 0, 0, instance, 0);
    }

    public static void Show()
    {
        var motion = Overlay.ResolveMotion();
        State.Refresh();
        var target = Layout();
        if (!_open)
        {
            Top.Snap(motion == MotionStyle.Slide ? target + _height : target);
            Alpha.Snap(0);
        }

        _open = true;
        _closing = false;
        Move(target, motion);
        Set(Alpha, 1, motion);
        Apply();
        OptionsPanel.TakeFocus(_window);
        StartClock();
    }

    /// <summary>A lista mudou: acompanha, se estiver aberto.</summary>
    public static void Refresh()
    {
        if (_open && !_closing)
        {
            State.Refresh();
            Move(Layout(), MotionStyle.Instant);
            Apply();
        }
    }

    /// <summary>Sai pelo mesmo caminho por onde entrou.</summary>
    private static void Close()
    {
        if (!_open || _closing)
        {
            return;
        }

        var motion = Overlay.ResolveMotion();
        _closing = true;
        Set(Alpha, 0, motion);
        if (motion == MotionStyle.Slide)
        {
            Top.Retarget(Top.Value + _height);
        }

        Apply();
        StartClock();
    }

    private static int Layout()
    {
        Notifications.ReadWorkArea();
        var area = Notifications.WorkArea;
        var width = Notifications.Scale(FilesPanelState.Width);
        _height = Notifications.Scale(State.Height);
        _left = area.Right - Notifications.Scale(Margin) - width;
        return area.Bottom - Notifications.Scale(Margin) - _height;
    }

    private static void Move(int target, MotionStyle motion) => Set(Top, target, motion == MotionStyle.Slide ? motion : MotionStyle.Instant);

    private static void Set(CardTransition transition, double value, MotionStyle motion)
    {
        if (motion == MotionStyle.Instant)
        {
            transition.Snap(value);
        }
        else
        {
            transition.Retarget(value);
        }
    }

    private static void Apply()
    {
        if (_closing && Alpha.IsDone && Top.IsDone)
        {
            _open = false;
            _closing = false;
            ShowWindow(_window, SW_HIDE);
            return;
        }

        Render();

        // O foco é pedido só uma vez, em Show: aqui não pode voltar a tomá-lo enquanto o painel fecha.
        SetWindowPos(_window, HWND_TOPMOST, _left, (int)Top.Value, Notifications.Scale(FilesPanelState.Width), _height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private static void Render() =>
        DiscRenderer.ShowFiles(_window, _left, (int)Top.Value, Notifications.Scale(FilesPanelState.Width), _height, Notifications.Dpi, State, Notifications.History.Items, DiscRenderer.DarkTheme(), Alpha.Value);

    private static void StartClock()
    {
        _lastFrame = Stopwatch.GetTimestamp();
        SetTimer(_window, TimerFrame, FrameMs, 0);
    }

    private static void Frame()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastFrame, now).TotalMilliseconds;
        _lastFrame = now;
        Top.Advance(elapsed);
        Alpha.Advance(elapsed);
        Apply();
        if (Top.IsDone && Alpha.IsDone)
        {
            KillTimer(_window, TimerFrame);
        }
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case WM_ERASEBKGND:
                return 1;

            case WM_TIMER:
                Frame();
                return 0;

            case WM_ACTIVATE:
                if ((int)(wParam & 0xFFFF) == WaInactive)
                {
                    Close();
                }

                return 0;

            case WM_CLOSE:
                // Alt+F4: a janela é reaproveitada e não pode ser destruída.
                Close();
                return 0;

            case WM_KEYDOWN:
                OnKey((int)wParam);
                return 0;

            case WM_MOUSEWHEEL:
                State.Wheel((short)((wParam >> 16) & 0xFFFF));
                Render();
                return 0;

            case WM_LBUTTONDOWN:
                Run(State.Click(Logical((short)(lParam & 0xFFFF)), Logical((short)((lParam >> 16) & 0xFFFF))));
                return 0;

            default:
                return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }

    private static int Logical(int pixels) => pixels * 96 / Notifications.Dpi;

    private static void OnKey(int virtualKey)
    {
        if (OptionsPanel.ToKey(virtualKey) is not { } key)
        {
            return;
        }

        if (key == PanelKey.Escape)
        {
            Close();
            return;
        }

        Run(State.Press(key));
    }

    private static void Run((long Id, NotificationAction Action)? command)
    {
        if (command is { } action)
        {
            Notifications.Perform(action.Id, action.Action);
        }

        if (_open && !_closing)
        {
            // O clique pode ter aberto outra linha: a altura não muda, só o desenho.
            Render();
        }
    }
}
