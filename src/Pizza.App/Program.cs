using System.Runtime.InteropServices;
using Pizza.Core.Diagnostics;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

internal static unsafe class Program
{
    /// <summary>
    /// Ponto de entrada do residente. Na etapa 1a é a prova de conceito do gesto; a bandeja chega na 1b.
    /// Enquanto não há bandeja, Ctrl + Alt + Shift + P encerra o programa.
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

        // Sem bandeja, o atalho é a única saída: se não registrar, não fica rodando sem como fechar.
        if (RegisterHotKey(Overlay.Disc, Overlay.HotkeyQuit, MOD_CONTROL | MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, VK_P) == 0)
        {
            Overlay.Destroy();
            OleUninitialize();
            return 1;
        }

        GestureHook.Install();
        Log.Write(LogEvent.AppStarted);

        MSG message;
        while (GetMessageW(&message, 0, 0, 0) > 0)
        {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }

        GestureHook.Uninstall();
        Overlay.Destroy();
        OleUninitialize();
        Log.Write(LogEvent.AppStopped);
        return 0;
    }
}
