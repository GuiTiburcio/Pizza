using System.Runtime.InteropServices;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

internal static unsafe class Program
{
    /// <summary>
    /// Ponto de entrada do residente. Na etapa 1a é a prova de conceito do gesto; a bandeja chega na 1b.
    /// Enquanto não há bandeja, Ctrl + Alt + F12 encerra o programa.
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
        GestureHook.Install();
        RegisterHotKey(Overlay.Disc, Overlay.HotkeyQuit, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_F12);
        Log.Write("residente iniciado");

        MSG message;
        while (GetMessageW(&message, 0, 0, 0) > 0)
        {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }

        GestureHook.Uninstall();
        Overlay.Destroy();
        OleUninitialize();
        Log.Write("residente encerrado");
        return 0;
    }
}
