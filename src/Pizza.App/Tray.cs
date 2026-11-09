using System.Runtime.InteropServices;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>Ícone do Pizza na área de notificação (ícones ocultos) e seu menu do clique direito.</summary>
internal static unsafe class Tray
{
    private const string ClassName = "PizzaTray";
    private const uint WmTrayCallback = WM_APP + 1;
    private const uint IconId = 1;
    private const nuint CommandOpenLogs = 1;
    private const nuint CommandQuit = 2;
    private const nuint CommandFiles = 3;

    // Identificador que o SDK do .NET dá ao ícone definido em ApplicationIcon.
    private const nint IconResourceId = 32512;

    private static nint _window;
    private static nint _icon;
    private static uint _wmTaskbarCreated;

    /// <summary>Cria o ícone. Sem ele o programa não teria como ser fechado, então quem chama deve encerrar.</summary>
    public static bool Create()
    {
        var instance = GetModuleHandleW(null);
        fixed (char* name = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                CbSize = (uint)sizeof(WNDCLASSEXW),
                LpfnWndProc = &WndProc,
                HInstance = instance,
                LpszClassName = name,
            };
            RegisterClassExW(&wc);
        }

        _window = CreateWindowExW(WS_EX_TOOLWINDOW, ClassName, "Pizza", WS_POPUP, 0, 0, 0, 0, 0, 0, instance, 0);
        _icon = LoadImageW(instance, IconResourceId, IMAGE_ICON, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), 0);
        _wmTaskbarCreated = RegisterWindowMessageW("TaskbarCreated");
        return _icon != 0 && AddIcon();
    }

    public static void Remove()
    {
        var data = NewData();
        Shell_NotifyIconW(NIM_DELETE, &data);
    }

    private static NOTIFYICONDATAW NewData() => new()
    {
        CbSize = (uint)sizeof(NOTIFYICONDATAW),
        HWnd = _window,
        UID = IconId,
    };

    private static bool AddIcon()
    {
        var data = NewData();
        data.UFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP;
        data.UCallbackMessage = WmTrayCallback;
        data.HIcon = _icon;
        "Pizza".AsSpan().CopyTo(new Span<char>(data.SzTip, 128));
        if (Shell_NotifyIconW(NIM_ADD, &data) == 0)
        {
            return false;
        }

        data.UVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIconW(NIM_SETVERSION, &data);
        return true;
    }

    private static void ShowMenu(int x, int y)
    {
        var menu = CreatePopupMenu();
        AppendMenuW(menu, MF_STRING | MF_GRAYED, 0, "Pizza — ativo");
        AppendMenuW(menu, MF_SEPARATOR, 0, null);
        AppendMenuW(menu, MF_STRING, CommandFiles, "Arquivos...");
        AppendMenuW(menu, MF_STRING, CommandOpenLogs, "Abrir pasta de logs");
        AppendMenuW(menu, MF_SEPARATOR, 0, null);
        AppendMenuW(menu, MF_STRING, CommandQuit, "Sair");

        // Sem isto, o menu não fecha ao clicar fora dele.
        SetForegroundWindow(_window);
        TrackPopupMenu(menu, TPM_RIGHTBUTTON, x, y, 0, _window, null);
        PostMessageW(_window, WM_NULL, 0, 0);
        DestroyMenu(menu);
    }

    public static void OpenLogs()
    {
        try
        {
            Directory.CreateDirectory(Log.Folder);
            ShellExecuteW(0, "open", Log.Folder, null, null, SW_SHOWNORMAL);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sem a pasta não há o que abrir; o residente segue rodando.
        }
    }

    [UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        if (msg == _wmTaskbarCreated)
        {
            // O Explorer reiniciou e perdeu os ícones: sem isto, o Pizza ficaria sem como ser fechado.
            AddIcon();
            return 0;
        }

        switch (msg)
        {
            case WmTrayCallback:
                if ((lParam & 0xFFFF) == WM_CONTEXTMENU)
                {
                    ShowMenu((short)(wParam & 0xFFFF), (short)((wParam >> 16) & 0xFFFF));
                }

                return 0;

            case WM_COMMAND:
                if ((wParam & 0xFFFF) == CommandFiles)
                {
                    FilesPanel.Show();
                }
                else if ((wParam & 0xFFFF) == CommandOpenLogs)
                {
                    OpenLogs();
                }
                else if ((wParam & 0xFFFF) == CommandQuit)
                {
                    PostQuitMessage(0);
                }

                return 0;

            default:
                return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }
}
