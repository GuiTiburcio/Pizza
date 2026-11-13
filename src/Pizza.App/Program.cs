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
        // PRIV-05 (ADR-032): antes de tudo, para que nenhum travamento vire dump ou relatório do Windows.
        CrashReporting.Suppress();
        using var instance = new Mutex(initiallyOwned: true, @"Local\Pizza.Residente", out var isFirst);
        if (!isFirst)
        {
            return 0;
        }

        CrashReporting.ExcludeFromWer([Environment.ProcessPath!, Path.Combine(AppContext.BaseDirectory, "Pizza.Worker.exe")]);
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        Marshal.ThrowExceptionForHR(OleInitialize(0));

        DiscRenderer.Start();
        Overlay.Create();
        OptionsPanel.Create();
        Notifications.Create();
        FilesPanel.Create();
        TrayMenu.Create();

        // O ícone é a única saída: sem ele, o programa não fica rodando sem como fechar.
        if (!Tray.Create())
        {
            Overlay.Destroy();
            OleUninitialize();
            return 1;
        }

        Conversion.Initialize();
        Overlay.MenuOpened = Conversion.WarmUp;
        GestureHook.Install();
        Log.Write(LogEvent.AppStarted);

        // NFR-01: o hook do mouse não passa por aqui; só mensagens das janelas do Pizza e da conversão adiam a coleta.
        var idle = new IdleCollection();
        nuint idleTimer = 0;
        MSG message;
        while (GetMessageW(&message, 0, 0, 0) > 0)
        {
            var isIdleTimer = message.Message == WM_TIMER && message.Hwnd == 0 && idleTimer != 0 && message.WParam == idleTimer;
            switch (idle.OnMessage(isIdleTimer))
            {
                case IdleStep.Rearm:
                    idleTimer = SetTimer(0, idleTimer, (uint)IdleCollection.Delay.TotalMilliseconds, 0);
                    break;

                case IdleStep.Collect:
                    KillTimer(0, idleTimer);
                    idleTimer = 0;
                    GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                    break;
            }

            if (isIdleTimer)
            {
                continue;
            }

            TranslateMessage(&message);
            DispatchMessageW(&message);
        }

        GestureHook.Uninstall();
        Conversion.Shutdown();
        Tray.Remove();
        Overlay.Destroy();
        OleUninitialize();
        Log.Write(LogEvent.AppStopped);
        return 0;
    }
}
