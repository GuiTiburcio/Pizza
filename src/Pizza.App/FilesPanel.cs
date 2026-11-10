using System.Diagnostics;
using Pizza.Core.Conversions;
using Pizza.Core.Gesture;
using Pizza.Core.Notifications;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// Painel "Arquivos..." (FR-44), no canto inferior direito. Fecha ao perder o foco ou com Esc.
/// Fundo sólido por enquanto: o vidro é da Fase 4.
/// </summary>
internal static unsafe class FilesPanel
{
    private const string ClassName = "PizzaFiles";
    private const nuint TimerFrame = 1;
    private const uint FrameMs = 16;
    private const byte PanelAlpha = 245;
    private const int Radius = 16;
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
        InvalidateRect(_window, null, 0);
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
            InvalidateRect(_window, null, 0);
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
        var radius = Notifications.Scale(Radius) * 2;
        SetWindowRgn(_window, CreateRoundRectRgn(0, 0, width + 1, _height + 1, radius, radius), 1);
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

        SetLayeredWindowAttributes(_window, 0, (byte)(PanelAlpha * Alpha.Value), LWA_ALPHA);

        // O foco é pedido só uma vez, em Show: aqui não pode voltar a tomá-lo enquanto o painel fecha.
        SetWindowPos(_window, HWND_TOPMOST, _left, (int)Top.Value, Notifications.Scale(FilesPanelState.Width), _height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

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

            case WM_PAINT:
                Paint(hwnd);
                return 0;

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
                InvalidateRect(hwnd, null, 0);
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

        InvalidateRect(_window, null, 0);
    }

    private static void Paint(nint hwnd)
    {
        PAINTSTRUCT ps;
        var hdc = BeginPaint(hwnd, &ps);
        var width = Notifications.Scale(FilesPanelState.Width);
        var height = Notifications.Scale(State.Height);
        var buffer = CreateCompatibleDC(hdc);
        var bitmap = CreateCompatibleBitmap(hdc, width, height);
        var oldBitmap = SelectObject(buffer, bitmap);

        Draw(buffer, width, height);
        BitBlt(hdc, 0, 0, width, height, buffer, 0, 0, SRCCOPY);

        SelectObject(buffer, oldBitmap);
        DeleteObject(bitmap);
        DeleteDC(buffer);
        EndPaint(hwnd, &ps);
    }

    private static void Draw(nint hdc, int width, int height)
    {
        Fill(hdc, new RECT { Right = width, Bottom = height }, Notifications.White);
        SetBkMode(hdc, TRANSPARENT);

        var items = Notifications.History.Items;
        var header = Notifications.Scale(FilesPanelState.HeaderHeight);
        if (items.Count == 0)
        {
            Notifications.Label(hdc, "Nenhuma conversão ainda", new RECT { Top = header, Right = width, Bottom = height }, 13, FW_NORMAL, Notifications.Body, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
        }

        for (var row = 0; row < items.Count; row++)
        {
            var top = FilesPanelState.HeaderHeight + (row * FilesPanelState.RowHeight) - State.Scroll;
            if (top + FilesPanelState.RowHeight > 0 && Notifications.Scale(top) < height)
            {
                DrawRow(hdc, items[row], row, top, width);
            }
        }

        // O cabeçalho vem por último, cobrindo as linhas que sobem com a rolagem.
        Fill(hdc, new RECT { Right = width, Bottom = header }, Notifications.White);
        Notifications.Label(hdc, "Arquivos", new RECT { Left = Notifications.Scale(16), Right = width, Bottom = header }, 14, FW_BOLD, Notifications.Ink, DT_SINGLELINE | DT_VCENTER);
        Fill(hdc, new RECT { Top = header - 1, Right = width, Bottom = header }, Notifications.Line);
        DrawScrollThumb(hdc, width, height, header);
    }

    private static void DrawRow(nint hdc, HistoryEntry entry, int row, int top, int width)
    {
        var inner = width - Notifications.Scale(16);
        var left = Notifications.Scale(16);
        var name = new RECT { Left = left, Top = Notifications.Scale(top + 10), Right = inner, Bottom = Notifications.Scale(top + 32) };
        Notifications.Label(hdc, entry.Name, name, 13, FW_SEMIBOLD, Notifications.Ink, DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);

        var status = new RECT { Left = left, Top = Notifications.Scale(top + 32), Right = inner, Bottom = Notifications.Scale(top + 52) };
        var problem = entry.State == HistoryState.Failed || entry.Missing;
        Notifications.Label(hdc, StatusText(entry), status, 12, FW_NORMAL, problem ? Notifications.Failure : Notifications.Body, DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);

        var primary = true;
        foreach (var button in FilesPanelState.Buttons(entry.State))
        {
            var bounds = new RECT
            {
                Left = Notifications.Scale(button.Bounds.Left),
                Top = Notifications.Scale(top + button.Bounds.Top),
                Right = Notifications.Scale(button.Bounds.Right),
                Bottom = Notifications.Scale(top + button.Bounds.Bottom),
            };
            Notifications.Round(hdc, bounds, Notifications.Scale(12), primary ? Notifications.Accent : Notifications.Soft);
            Notifications.Label(hdc, button.Label, bounds, 13, FW_SEMIBOLD, primary ? Notifications.White : Notifications.Ink, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
            if (State.Focused == (row, button.Action))
            {
                Frame(hdc, bounds);
            }

            primary = false;
        }

        Fill(hdc, new RECT { Top = Notifications.Scale(top + FilesPanelState.RowHeight) - 1, Right = width, Bottom = Notifications.Scale(top + FilesPanelState.RowHeight) }, Notifications.Line);
    }

    private static string StatusText(HistoryEntry entry) => entry.State switch
    {
        HistoryState.Running => "Convertendo…",
        HistoryState.Failed => entry.Message!,
        _ => entry.Missing ? "Arquivo não encontrado" : "Concluído",
    };

    private static void DrawScrollThumb(nint hdc, int width, int height, int header)
    {
        if (State.MaxScroll == 0)
        {
            return;
        }

        var track = height - header;
        var thumb = Math.Max(Notifications.Scale(24), track * State.ViewportHeight / State.ContentHeight);
        var offset = (track - thumb) * State.Scroll / State.MaxScroll;
        var bar = new RECT { Left = width - Notifications.Scale(6), Top = header + offset, Right = width - Notifications.Scale(2), Bottom = header + offset + thumb };
        Notifications.Round(hdc, bar, Notifications.Scale(2), Notifications.Line);
    }

    private static void Fill(nint hdc, RECT rect, uint color)
    {
        var brush = CreateSolidBrush(color);
        FillRect(hdc, &rect, brush);
        DeleteObject(brush);
    }

    private static void Frame(nint hdc, RECT rect)
    {
        var brush = CreateSolidBrush(Notifications.Ink);
        FrameRect(hdc, &rect, brush);
        var inside = new RECT { Left = rect.Left + 1, Top = rect.Top + 1, Right = rect.Right - 1, Bottom = rect.Bottom - 1 };
        FrameRect(hdc, &inside, brush);
        DeleteObject(brush);
    }
}
