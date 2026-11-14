using Pizza.Core.Conversions;
using Pizza.Core.Notifications;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// Menu do clique direito no ícone (ADR-020, prancha 4d-2), em vidro translúcido como Arquivos. Lê o teclado só
/// pelas mensagens da própria janela, sem hook (SEC-01), e fecha ao perder o foco ou com Esc.
/// </summary>
internal static unsafe class TrayMenu
{
    private const string ClassName = "PizzaTrayMenu";
    private const int WaInactive = 0;

    private static TrayMenuState _state = new();
    private static nint _window;
    private static bool _open;
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

        _window = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_LAYERED, ClassName, "Pizza Menu", WS_POPUP, 0, 0, 1, 1, 0, 0, instance, 0);
    }

    /// <summary>Abre junto ao cursor, acima e à esquerda, inteiro na área de trabalho do monitor.</summary>
    public static void Show(int x, int y)
    {
        _state = new TrayMenuState();
        var monitor = MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { CbSize = (uint)sizeof(MONITORINFO) };
        GetMonitorInfoW(monitor, &info);
        _dpi = MenuSettings.Dpi(info.RcWork); // ADR-037: 1/15 da tela, como o disco.
        var area = new PanelRect(info.RcWork.Left, info.RcWork.Top, info.RcWork.Right, info.RcWork.Bottom);
        (_left, _top) = TrayMenuState.Place(x, y, Scale(TrayMenuState.Width), Scale(_state.Height), area);

        _open = true;
        Render();
        SetWindowPos(_window, HWND_TOPMOST, _left, _top, Scale(TrayMenuState.Width), Scale(_state.Height), SWP_SHOWWINDOW);
        OptionsPanel.TakeFocus(_window);
    }

    private static void Hide()
    {
        if (_open)
        {
            _open = false;
            ShowWindow(_window, SW_HIDE);
        }
    }

    private static void Run(TrayCommand? command)
    {
        switch (command)
        {
            case null:
                Render();
                return;
            case TrayCommand.Files:
                Hide();
                FilesPanel.Show();
                return;
            case TrayCommand.OpenLogs:
                Hide();
                Tray.OpenLogs();
                return;
            case TrayCommand.Quit:
                Hide();
                PostQuitMessage(0);
                return;
            default:
                Hide();
                return;
        }
    }

    private static void Render()
    {
        if (_open)
        {
            DiscRenderer.ShowTrayMenu(_window, _left, _top, Scale(TrayMenuState.Width), Scale(_state.Height), _dpi, _state, DiscRenderer.DarkTheme());
        }
    }

    private static int Scale(int value) => value * _dpi / 96;

    private static int Logical(nint lParam, int shift) => (short)((lParam >> shift) & 0xFFFF) * 96 / _dpi;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case WM_ACTIVATE:
                if ((int)(wParam & 0xFFFF) == WaInactive)
                {
                    Hide();
                }

                return 0;

            case WM_CLOSE:
                // Alt+F4: a janela é reaproveitada e não pode ser destruída.
                Hide();
                return 0;

            case WM_KEYDOWN:
                if (OptionsPanel.ToKey((int)wParam) is { } key)
                {
                    Run(_state.Press(key));
                }

                return 0;

            case WM_MOUSEMOVE:
                if (_state.Hover(Logical(lParam, 0), Logical(lParam, 16)))
                {
                    Render();
                }

                return 0;

            case WM_LBUTTONUP:
                if (_state.Click(Logical(lParam, 0), Logical(lParam, 16)) is { } command)
                {
                    Run(command);
                }

                return 0;

            default:
                return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }
}
