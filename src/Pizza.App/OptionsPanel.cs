using System.Diagnostics;
using Pizza.Core.Conversions;
using Pizza.Core.Gesture;
using Pizza.Core.Visual;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// Painel de opções (WF-3) em vidro, com a bolha na opção escolhida de cada grupo (ADR-027, prancha 4b-3).
/// Lê o teclado só pelas mensagens da própria janela, sem nenhum hook (SEC-01).
/// </summary>
internal static unsafe class OptionsPanel
{
    private const string ClassName = "PizzaPanel";
    private const nuint TimerBubble = 1;
    private const uint AnimationMs = 10;

    private static PanelPalette _palette = PanelPalette.Light;

    private static nint _window;
    private static OptionsPanelState? _state;
    private static PanelViewport? _viewport;
    private static GlideSpring[] _bubbles = [];
    private static MotionStyle _motion = MotionStyle.Slide;
    private static long _lastFrame;
    private static int _wheelRemainder;
    private static string _fileName = string.Empty;
    private static int _dpi = 96;
    private static int _left;
    private static int _top;

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

        // Em camadas: os cantos e o vidro têm transparência por pixel, como o menu (ADR-023).
        _window = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_LAYERED, ClassName, "Pizza Opções", WS_POPUP, 0, 0, 1, 1, 0, 0, instance, 0);
    }

    /// <summary>Abre centrado no ponto, inteiro na área de trabalho do monitor.</summary>
    public static void Show(ConverterInfo converter, string fileName, int centerX, int centerY)
    {
        _state = new OptionsPanelState(converter);
        _fileName = fileName;
        _palette = DarkTheme() ? PanelPalette.Dark : PanelPalette.Light;
        _motion = Overlay.ResolveMotion();
        _bubbles = Enumerable.Range(0, _state.Rows.Count).Select(row =>
        {
            var glide = new GlideSpring();
            glide.Snap(_state.Selected(row));
            return glide;
        }).ToArray();

        var monitor = MonitorFromPoint(new POINT { X = centerX, Y = centerY }, MONITOR_DEFAULTTONEAREST);
        uint dpiX, dpiY;
        _dpi = GetDpiForMonitor(monitor, 0, &dpiX, &dpiY) == S_OK ? (int)dpiX : 96;
        var info = new MONITORINFO { CbSize = (uint)sizeof(MONITORINFO) };
        GetMonitorInfoW(monitor, &info);
        _viewport = new PanelViewport(_state.Height, (info.RcWork.Bottom - info.RcWork.Top) * 96 / _dpi);
        _wheelRemainder = 0;

        var width = Scale(_state.Width);
        var height = Scale(_viewport.Height);
        _left = Math.Max(info.RcWork.Left, Math.Min(centerX - (width / 2), info.RcWork.Right - width));
        _top = Math.Max(info.RcWork.Top, Math.Min(centerY - (height / 2), info.RcWork.Bottom - height));

        // O menu acabou de sumir: espera a tela ser recomposta para ele não entrar na cópia do vidro.
        DwmFlush();
        DiscRenderer.Prepare(_left, _top, width, height, _dpi);
        Render();
        SetWindowPos(_window, HWND_TOPMOST, _left, _top, width, height, SWP_SHOWWINDOW);
        TakeFocus(_window);
    }

    /// <summary>Fecha sem avisar o overlay: é ele quem está pedindo. A cópia da tela é zerada aqui (ADR-023).</summary>
    public static void Close()
    {
        if (_state is not null)
        {
            _state = null;
            _viewport = null;
            KillTimer(_window, TimerBubble);
            _lastFrame = 0;
            ShowWindow(_window, SW_HIDE);
            DiscRenderer.Release();
        }
    }

    /// <summary>Enter e Esc precisam do foco, mas quem estava em primeiro plano era o Explorer, no arraste.</summary>
    public static void TakeFocus(nint window)
    {
        if (SetForegroundWindow(window) == 0 || GetForegroundWindow() != window)
        {
            var theirs = GetWindowThreadProcessId(GetForegroundWindow(), null);
            var ours = GetCurrentThreadId();
            if (theirs != 0 && theirs != ours && AttachThreadInput(ours, theirs, 1) != 0)
            {
                SetForegroundWindow(window);
                AttachThreadInput(ours, theirs, 0);
            }
        }

        SetFocus(window);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case WM_ERASEBKGND:
                return 1;

            case WM_KEYDOWN:
                if (ToKey((int)wParam) is { } key && _state is { } state)
                {
                    var action = state.Press(key);
                    var focus = state.Targets.First(target =>
                        (target.Kind, target.Row) == state.Focus &&
                        (target.Kind != PanelTargetKind.Choice || state.IsSelected(target.Row, target.Choice)));
                    _viewport?.Reveal(focus.Bounds);
                    Apply(action);
                }

                return 0;

            case WM_LBUTTONDOWN:
                OnClick((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF));
                return 0;

            case WM_MOUSEWHEEL:
                _wheelRemainder += (short)(wParam >> 16);
                Scroll(-(_wheelRemainder / 120) * 44);
                _wheelRemainder %= 120;
                return 0;

            case WM_TIMER when wParam == TimerBubble:
                AnimationFrame();
                return 0;

            case WM_CLOSE:
                // Alt+F4 vale como cancelar. Sem isto, o Windows destruiria a janela, que é reaproveitada.
                if (_state is not null)
                {
                    Apply(PanelAction.Cancel);
                }

                return 0;

            default:
                return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }

    public static PanelKey? ToKey(int virtualKey) => virtualKey switch
    {
        VK_TAB => GetKeyState(VK_SHIFT) < 0 ? PanelKey.ShiftTab : PanelKey.Tab,
        VK_LEFT => PanelKey.Left,
        VK_UP => PanelKey.Up,
        VK_RIGHT => PanelKey.Right,
        VK_DOWN => PanelKey.Down,
        VK_SPACE => PanelKey.Space,
        VK_RETURN => PanelKey.Enter,
        VK_ESCAPE => PanelKey.Escape,
        _ => null,
    };

    private static void OnClick(int x, int y)
    {
        if (_state is not { } state || _viewport is not { } viewport)
        {
            return;
        }

        x = x * 96 / _dpi;
        y = y * 96 / _dpi;
        if (viewport.CanScroll && y >= viewport.ContentHeight)
        {
            if (viewport.ScrollButton(state.Width, next: false).Contains(x, y))
            {
                Scroll(-44);
            }
            else if (viewport.ScrollButton(state.Width, next: true).Contains(x, y))
            {
                Scroll(44);
            }

            return;
        }

        if (state.TargetAt(x, y + viewport.Offset) is { } target)
        {
            Apply(state.Click(target));
        }
    }

    private static void Scroll(int distance)
    {
        _viewport?.Scroll(distance);
        Render();
    }

    private static void Apply(PanelAction action)
    {
        if (action == PanelAction.None)
        {
            GlideToSelection();
            return;
        }

        var options = action == PanelAction.Convert ? _state!.Options() : null;
        Close();
        Overlay.OnPanelClosed(options);
    }

    /// <summary>A bolha de cada grupo desliza até a opção escolhida, na mola do giro de aba (ADR-027).</summary>
    private static void GlideToSelection()
    {
        if (_state is not { } state)
        {
            return;
        }

        for (var row = 0; row < _bubbles.Length; row++)
        {
            if (_motion == MotionStyle.Slide)
            {
                _bubbles[row].Retarget(state.Selected(row));
            }
            else
            {
                _bubbles[row].Snap(state.Selected(row));
            }
        }

        Render();
        if (_lastFrame == 0 && _bubbles.Any(bubble => bubble.IsAnimating))
        {
            _lastFrame = Stopwatch.GetTimestamp();
            SetTimer(_window, TimerBubble, AnimationMs, 0);
        }
    }

    private static void AnimationFrame()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsedMs = Stopwatch.GetElapsedTime(_lastFrame, now).TotalMilliseconds;
        _lastFrame = now;
        foreach (var bubble in _bubbles)
        {
            bubble.Advance(elapsedMs);
        }

        Render();
        if (!_bubbles.Any(bubble => bubble.IsAnimating))
        {
            KillTimer(_window, TimerBubble);
            _lastFrame = 0;
        }
    }

    private static void Render()
    {
        if (_state is not { } state || _viewport is not { } viewport)
        {
            return;
        }

        var frame = new PanelFrame(
            state,
            viewport,
            _fileName,
            _bubbles.Select(bubble => bubble.Position).ToArray(),
            _bubbles.Select(bubble => bubble.Swell).ToArray(),
            _palette);
        DiscRenderer.ShowPanel(_window, _left, _top, frame);
    }

    private static int Scale(int value) => value * _dpi / 96;

    private static bool DarkTheme()
    {
        uint value = 1;
        var size = (uint)sizeof(uint);
        var result = RegGetValueW(HKEY_CURRENT_USER, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", RRF_RT_REG_DWORD, null, &value, &size);
        return result == 0 && value == 0;
    }
}
