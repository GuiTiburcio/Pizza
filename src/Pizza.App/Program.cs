using System.Runtime.InteropServices;
using Pizza.Core.Diagnostics;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

internal static unsafe class Program
{
    /// <summary>
    /// Ponto de entrada do residente: fica nos ícones ocultos e encerra pelo menu do ícone.
    /// </summary>
    [STAThread]
    private static int Main()
    {
        using var instance = new Mutex(initiallyOwned: true, @"Local\Pizza.Residente", out var isFirst);
        if (!isFirst)
        {
            return 0;
        }

        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        Marshal.ThrowExceptionForHR(OleInitialize(0));

        Overlay.Create();
        OptionsPanel.Create();
        Notifications.Create();
        FilesPanel.Create();

        // O ícone é a única saída: sem ele, o programa não fica rodando sem como fechar.
        if (!Tray.Create())
        {
            Overlay.Destroy();
            OleUninitialize();
            return 1;
        }

        Conversion.Initialize();
        GestureHook.Install();
        Log.Write(LogEvent.AppStarted);

        MSG message;
        while (GetMessageW(&message, 0, 0, 0) > 0)
        {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }

        GestureHook.Uninstall();
        Tray.Remove();
        Overlay.Destroy();
        OleUninitialize();
        Log.Write(LogEvent.AppStopped);
        return 0;
    }
}
