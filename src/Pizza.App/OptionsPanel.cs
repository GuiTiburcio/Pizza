using Pizza.Core.Conversions;
using Pizza.Core.Visual;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// Painel de opções (WF-3), com temas e cantos da Fase 4b. Vidro ainda pendente.
/// Lê o teclado só pelas mensagens da própria janela, sem nenhum hook (SEC-01).
/// </summary>
internal static unsafe class OptionsPanel
{
    private const string ClassName = "PizzaPanel";
    private const string MetadataNotice = "Este formato não guarda metadados";
    private const int KeepBoxSize = 18;
    private const int PanelRadius = 28;
    private const int ButtonRadius = 12;

    private static PanelPalette _palette = PanelPalette.Light;

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
        _palette = DarkTheme() ? PanelPalette.Dark : PanelPalette.Light;

        var monitor = MonitorFromPoint(new POINT { X = centerX, Y = centerY }, MONITOR_DEFAULTTONEAREST);
        uint dpiX, dpiY;
        _dpi = GetDpiForMonitor(monitor, 0, &dpiX, &dpiY) == S_OK ? (int)dpiX : 96;
        var info = new MONITORINFO { CbSize = (uint)sizeof(MONITORINFO) };
        GetMonitorInfoW(monitor, &info);

        var width = Scale(_state.Width);
        var height = Scale(_state.Height);
        var left = Math.Max(info.RcWork.Left, Math.Min(centerX - (width / 2), info.RcWork.Right - width));
        var top = Math.Max(info.RcWork.Top, Math.Min(centerY - (height / 2), info.RcWork.Bottom - height));

        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, Scale(PanelRadius * 2), Scale(PanelRadius * 2));
        // Em sucesso a região pertence ao Windows, inclusive quando é substituída ao reabrir.
        if (region != 0 && SetWindowRgn(_window, region, 0) == 0)
        {
            DeleteObject(region);
        }

        SetWindowPos(_window, HWND_TOPMOST, left, top, width, height, SWP_SHOWWINDOW);
        InvalidateRect(_window, null, 0);
        TakeFocus(_window);
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
        Fill(hdc, whole, _palette.Background);
        Round(hdc, whole, PanelRadius, _palette.Border);
        var inside = new RECT { Left = 1, Top = 1, Right = width - 1, Bottom = height - 1 };
        Round(hdc, inside, PanelRadius, _palette.Background);
        SetBkMode(hdc, TRANSPARENT);

        var inner = width - Scale(OptionsPanelState.Padding);
        var title = new RECT { Left = Scale(OptionsPanelState.Padding), Top = Scale(22), Right = inner, Bottom = Scale(52) };
        Label(hdc, $"{_fileName} → {state.Converter.Label}", title, 18, FW_BOLD, _palette.Text, DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
        var subtitle = new RECT { Left = title.Left, Top = Scale(54), Right = inner, Bottom = Scale(76) };
        Label(hdc, "Escolha como quer o arquivo", subtitle, 13, FW_NORMAL, _palette.SecondaryText, DT_SINGLELINE | DT_VCENTER);

        for (var row = 0; row < state.Rows.Count; row++)
        {
            var top = OptionsPanelState.RowLabelTop(row);
            var caption = new RECT { Left = title.Left, Top = Scale(top), Right = inner, Bottom = Scale(top + OptionsPanelState.LabelHeight) };
            Label(hdc, state.Rows[row].Label, caption, 13, FW_SEMIBOLD, _palette.Text, DT_SINGLELINE | DT_VCENTER);
        }

        foreach (var target in state.Targets)
        {
            DrawTarget(hdc, state, target);
        }

        if (!state.CanKeepMetadata)
        {
            DrawKeep(hdc, state.KeepTop, enabled: false, isChecked: false);
            var notice = new RECT { Left = title.Left, Top = Scale(state.KeepTop + OptionsPanelState.KeepRowHeight), Right = inner, Bottom = Scale(state.KeepTop + OptionsPanelState.KeepRowHeight + 20) };
            Label(hdc, MetadataNotice, notice, 12, FW_NORMAL, _palette.SecondaryText, DT_SINGLELINE | DT_VCENTER);
        }
    }

    private static void DrawTarget(nint hdc, OptionsPanelState state, PanelTarget target)
    {
        var bounds = Scaled(target.Bounds);
        switch (target.Kind)
        {
            case PanelTargetKind.Choice:
                var selected = state.IsSelected(target.Row, target.Choice);
                Round(hdc, bounds, ButtonRadius, selected ? _palette.Accent : _palette.Surface);

                // A cor nunca é o único sinal: a escolhida também ganha marca e negrito.
                var choice = state.Rows[target.Row].Choices[target.Choice].Label;
                Label(hdc, selected ? "✓ " + choice : choice, bounds, 13, selected ? FW_BOLD : FW_NORMAL, selected ? _palette.OnAccent : _palette.Text, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
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
                DrawButton(hdc, bounds, "Cancelar  Esc", _palette.Surface, _palette.Text, state.Focus.Kind == PanelTargetKind.Cancel);
                break;

            case PanelTargetKind.Convert:
                DrawButton(hdc, bounds, "Converter  Enter", _palette.Accent, _palette.OnAccent, state.Focus.Kind == PanelTargetKind.Convert);
                break;
        }
    }

    private static void DrawKeep(nint hdc, int top, bool enabled, bool isChecked)
    {
        var left = OptionsPanelState.Padding + 3;
        var boxTop = top + ((OptionsPanelState.KeepRowHeight - KeepBoxSize) / 2);
        var box = new RECT { Left = Scale(left), Top = Scale(boxTop), Right = Scale(left + KeepBoxSize), Bottom = Scale(boxTop + KeepBoxSize) };
        Fill(hdc, box, isChecked ? _palette.Accent : _palette.Background);
        Frame(hdc, box, enabled ? _palette.Text : _palette.Border);
        if (isChecked)
        {
            Label(hdc, "✓", box, 13, FW_BOLD, _palette.OnAccent, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
        }

        var caption = new RECT { Left = Scale(left + KeepBoxSize + 10), Top = Scale(top), Right = Scale(440), Bottom = Scale(top + OptionsPanelState.KeepRowHeight) };
        Label(hdc, "Manter metadados", caption, 13, FW_NORMAL, enabled ? _palette.Text : _palette.SecondaryText, DT_SINGLELINE | DT_VCENTER);
    }

    private static void DrawButton(nint hdc, RECT bounds, string text, uint color, uint textColor, bool focused)
    {
        Round(hdc, bounds, ButtonRadius, color);
        Label(hdc, text, bounds, 13, FW_SEMIBOLD, textColor, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
        if (focused)
        {
            FocusRing(hdc, bounds);
        }
    }

    private static void FocusRing(nint hdc, RECT bounds)
    {
        var pen = CreatePen(0, Scale(2), _palette.Text);
        var oldPen = SelectObject(hdc, pen);
        var oldBrush = SelectObject(hdc, GetStockObject(NULL_BRUSH));
        RoundRect(hdc, bounds.Left + 1, bounds.Top + 1, bounds.Right - 1, bounds.Bottom - 1, Scale(ButtonRadius * 2), Scale(ButtonRadius * 2));
        SelectObject(hdc, oldBrush);
        SelectObject(hdc, oldPen);
        DeleteObject(pen);
    }

    private static void Round(nint hdc, RECT rect, int radius, uint color)
    {
        var brush = CreateSolidBrush(color);
        var oldBrush = SelectObject(hdc, brush);
        var oldPen = SelectObject(hdc, GetStockObject(NULL_PEN));
        RoundRect(hdc, rect.Left, rect.Top, rect.Right + 1, rect.Bottom + 1, Scale(radius * 2), Scale(radius * 2));
        SelectObject(hdc, oldPen);
        SelectObject(hdc, oldBrush);
        DeleteObject(brush);
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

    private static bool DarkTheme()
    {
        uint value = 1;
        var size = (uint)sizeof(uint);
        var result = RegGetValueW(HKEY_CURRENT_USER, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", RRF_RT_REG_DWORD, null, &value, &size);
        return result == 0 && value == 0;
    }
}
