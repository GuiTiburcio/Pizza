using Pizza.Core.Conversions;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// Painel de opções (WF-3), aberto depois do drop. Visual provisório em GDI; o vidro é da Fase 4.
/// Lê o teclado só pelas mensagens da própria janela, sem nenhum hook (SEC-01).
/// </summary>
internal static unsafe class OptionsPanel
{
    private const string ClassName = "PizzaPanel";
    private const string MetadataNotice = "Este formato não guarda metadados";
    private const int KeepBoxSize = 18;

    private static readonly uint Background = Rgb(26, 33, 64);
    private static readonly uint Idle = Rgb(62, 72, 112);
    private static readonly uint Accent = Rgb(47, 107, 255);
    private static readonly uint Text = Rgb(255, 255, 255);
    private static readonly uint Muted = Rgb(170, 182, 214);
    private static readonly uint Border = Rgb(84, 96, 136);

    private static nint _window;
    private static OptionsPanelState? _state;
    private static string _fileName = string.Empty;
    private static int _dpi = 96;

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

        _window = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW, ClassName, "Pizza Opções", WS_POPUP, 0, 0, 1, 1, 0, 0, instance, 0);
    }

    /// <summary>Abre centrado no ponto, inteiro na área de trabalho do monitor.</summary>
    public static void Show(ConverterInfo converter, string fileName, int centerX, int centerY)
    {
        _state = new OptionsPanelState(converter);
        _fileName = fileName;

        var monitor = MonitorFromPoint(new POINT { X = centerX, Y = centerY }, MONITOR_DEFAULTTONEAREST);
        uint dpiX, dpiY;
        _dpi = GetDpiForMonitor(monitor, 0, &dpiX, &dpiY) == S_OK ? (int)dpiX : 96;
        var info = new MONITORINFO { CbSize = (uint)sizeof(MONITORINFO) };
        GetMonitorInfoW(monitor, &info);

        var width = Scale(_state.Width);
        var height = Scale(_state.Height);
        var left = Math.Max(info.RcWork.Left, Math.Min(centerX - (width / 2), info.RcWork.Right - width));
        var top = Math.Max(info.RcWork.Top, Math.Min(centerY - (height / 2), info.RcWork.Bottom - height));

        SetWindowPos(_window, HWND_TOPMOST, left, top, width, height, SWP_SHOWWINDOW);
        InvalidateRect(_window, null, 0);
        TakeFocus();
    }

    /// <summary>Fecha sem avisar o overlay: é ele quem está pedindo.</summary>
    public static void Close()
    {
        if (_state is not null)
        {
            _state = null;
            ShowWindow(_window, SW_HIDE);
        }
    }

    /// <summary>Enter e Esc precisam do foco, mas quem estava em primeiro plano era o Explorer, no arraste.</summary>
    private static void TakeFocus()
    {
        if (SetForegroundWindow(_window) == 0 || GetForegroundWindow() != _window)
        {
            var theirs = GetWindowThreadProcessId(GetForegroundWindow(), null);
            var ours = GetCurrentThreadId();
            if (theirs != 0 && theirs != ours && AttachThreadInput(ours, theirs, 1) != 0)
            {
                SetForegroundWindow(_window);
                AttachThreadInput(ours, theirs, 0);
            }
        }

        SetFocus(_window);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case WM_ERASEBKGND:
                return 1;

            case WM_PAINT:
                Paint(hwnd);
                return 0;

            case WM_KEYDOWN:
                if (ToKey((int)wParam) is { } key && _state is { } state)
                {
                    Apply(state.Press(key));
                }

                return 0;

            case WM_LBUTTONDOWN:
                OnClick((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF));
                return 0;

            default:
                return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }

    private static PanelKey? ToKey(int virtualKey) => virtualKey switch
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
        if (_state is { } state && state.TargetAt(x * 96 / _dpi, y * 96 / _dpi) is { } target)
        {
            Apply(state.Click(target));
        }
    }

    private static void Apply(PanelAction action)
    {
        if (action == PanelAction.None)
        {
            InvalidateRect(_window, null, 0);
            return;
        }

        var options = action == PanelAction.Convert ? _state!.Options() : null;
        Close();
        Overlay.OnPanelClosed(options);
    }

    private static void Paint(nint hwnd)
    {
        PAINTSTRUCT ps;
        var hdc = BeginPaint(hwnd, &ps);
        if (_state is { } state)
        {
            var width = Scale(state.Width);
            var height = Scale(state.Height);
            var buffer = CreateCompatibleDC(hdc);
            var bitmap = CreateCompatibleBitmap(hdc, width, height);
            var oldBitmap = SelectObject(buffer, bitmap);
            Draw(buffer, state, width, height);
            BitBlt(hdc, 0, 0, width, height, buffer, 0, 0, SRCCOPY);
            SelectObject(buffer, oldBitmap);
            DeleteObject(bitmap);
            DeleteDC(buffer);
        }

        EndPaint(hwnd, &ps);
    }

    private static void Draw(nint hdc, OptionsPanelState state, int width, int height)
    {
        var whole = new RECT { Right = width, Bottom = height };
        Fill(hdc, whole, Background);
        Frame(hdc, whole, Border);
        SetBkMode(hdc, TRANSPARENT);

        var inner = width - Scale(OptionsPanelState.Padding);
        var title = new RECT { Left = Scale(OptionsPanelState.Padding), Top = Scale(22), Right = inner, Bottom = Scale(52) };
        Label(hdc, $"{_fileName} → {state.Converter.Label}", title, 18, FW_BOLD, Text, DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
        var subtitle = new RECT { Left = title.Left, Top = Scale(54), Right = inner, Bottom = Scale(76) };
        Label(hdc, "Escolha como quer o arquivo", subtitle, 13, FW_NORMAL, Muted, DT_SINGLELINE | DT_VCENTER);

        for (var row = 0; row < state.Rows.Count; row++)
        {
            var top = OptionsPanelState.RowLabelTop(row);
            var caption = new RECT { Left = title.Left, Top = Scale(top), Right = inner, Bottom = Scale(top + OptionsPanelState.LabelHeight) };
            Label(hdc, state.Rows[row].Label, caption, 13, FW_SEMIBOLD, Text, DT_SINGLELINE | DT_VCENTER);
        }

        foreach (var target in state.Targets)
        {
            DrawTarget(hdc, state, target);
        }

        if (!state.CanKeepMetadata)
        {
            DrawKeep(hdc, state.KeepTop, enabled: false, isChecked: false);
            var notice = new RECT { Left = title.Left, Top = Scale(state.KeepTop + 24), Right = inner, Bottom = Scale(state.KeepTop + 44) };
            Label(hdc, MetadataNotice, notice, 12, FW_NORMAL, Muted, DT_SINGLELINE | DT_VCENTER);
        }
    }

    private static void DrawTarget(nint hdc, OptionsPanelState state, PanelTarget target)
    {
        var bounds = Scaled(target.Bounds);
        switch (target.Kind)
        {
            case PanelTargetKind.Choice:
                var selected = state.IsSelected(target.Row, target.Choice);
                Fill(hdc, bounds, selected ? Accent : Idle);

                // A cor nunca é o único sinal: a escolhida também ganha marca e negrito.
                var choice = state.Rows[target.Row].Choices[target.Choice].Label;
                Label(hdc, selected ? "✓ " + choice : choice, bounds, 13, selected ? FW_BOLD : FW_NORMAL, Text, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
                if (selected && state.Focus == (PanelTargetKind.Choice, target.Row))
                {
                    FocusRing(hdc, bounds);
                }

                break;

            case PanelTargetKind.Keep:
                DrawKeep(hdc, target.Bounds.Top, enabled: true, state.KeepMetadata);
                if (state.Focus.Kind == PanelTargetKind.Keep)
                {
                    FocusRing(hdc, bounds);
                }

                break;

            case PanelTargetKind.Cancel:
                DrawButton(hdc, bounds, "Cancelar  Esc", Idle, state.Focus.Kind == PanelTargetKind.Cancel);
                break;

            case PanelTargetKind.Convert:
                DrawButton(hdc, bounds, "Converter  Enter", Accent, state.Focus.Kind == PanelTargetKind.Convert);
                break;
        }
    }

    private static void DrawKeep(nint hdc, int top, bool enabled, bool isChecked)
    {
        var left = OptionsPanelState.Padding + 3;
        var boxTop = top + 3;
        var box = new RECT { Left = Scale(left), Top = Scale(boxTop), Right = Scale(left + KeepBoxSize), Bottom = Scale(boxTop + KeepBoxSize) };
        Fill(hdc, box, isChecked ? Accent : Background);
        Frame(hdc, box, enabled ? Text : Border);
        if (isChecked)
        {
            Label(hdc, "✓", box, 13, FW_BOLD, Text, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
        }

        var caption = new RECT { Left = Scale(left + KeepBoxSize + 10), Top = Scale(top), Right = Scale(440), Bottom = Scale(top + 24) };
        Label(hdc, "Manter metadados", caption, 13, FW_NORMAL, enabled ? Text : Muted, DT_SINGLELINE | DT_VCENTER);
    }

    private static void DrawButton(nint hdc, RECT bounds, string text, uint color, bool focused)
    {
        Fill(hdc, bounds, color);
        Label(hdc, text, bounds, 13, FW_SEMIBOLD, Text, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
        if (focused)
        {
            FocusRing(hdc, bounds);
        }
    }

    private static void FocusRing(nint hdc, RECT bounds)
    {
        Frame(hdc, bounds, Text);
        var inside = new RECT { Left = bounds.Left + 1, Top = bounds.Top + 1, Right = bounds.Right - 1, Bottom = bounds.Bottom - 1 };
        Frame(hdc, inside, Text);
    }

    private static void Fill(nint hdc, RECT rect, uint color)
    {
        var brush = CreateSolidBrush(color);
        FillRect(hdc, &rect, brush);
        DeleteObject(brush);
    }

    private static void Frame(nint hdc, RECT rect, uint color)
    {
        var brush = CreateSolidBrush(color);
        FrameRect(hdc, &rect, brush);
        DeleteObject(brush);
    }

    private static void Label(nint hdc, string text, RECT rect, int fontSize, int weight, uint color, uint format)
    {
        var font = CreateFontW(-Scale(fontSize), 0, 0, 0, weight, 0, 0, 0, 1, 0, 0, CLEARTYPE_QUALITY, 0, "Segoe UI");
        var old = SelectObject(hdc, font);
        SetTextColor(hdc, color);
        DrawTextW(hdc, text, -1, &rect, format);
        SelectObject(hdc, old);
        DeleteObject(font);
    }

    private static RECT Scaled(PanelRect rect) => new()
    {
        Left = Scale(rect.Left),
        Top = Scale(rect.Top),
        Right = Scale(rect.Right),
        Bottom = Scale(rect.Bottom),
    };

    private static int Scale(int value) => value * _dpi / 96;
}
