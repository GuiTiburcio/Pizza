using System.Runtime.InteropServices;
using Pizza.Core.Gesture;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// Hook global de mouse; não existe hook de teclado (SEC-01). Só detecta e avisa o overlay por
/// mensagem, sem I/O nem registro de coordenadas (SEC-04).
/// </summary>
internal static unsafe class GestureHook
{
    private static readonly Func<bool> IsCtrlDown = static () => (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

    private static DragGestureDetector _detector = null!;
    private static nint _mouseHook;

    public static void Install()
    {
        _detector = new DragGestureDetector(GetSystemMetrics(SM_CXDRAG), GetSystemMetrics(SM_CYDRAG));
        _mouseHook = SetWindowsHookExW(WH_MOUSE_LL, &MouseProc, GetModuleHandleW(null), 0);
        if (_mouseHook == 0)
        {
            throw new InvalidOperationException("Não foi possível instalar o hook de mouse.");
        }
    }

    public static void Uninstall() => UnhookWindowsHookEx(_mouseHook);

    [UnmanagedCallersOnly]
    private static nint MouseProc(int code, nuint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var data = (MSLLHOOKSTRUCT*)lParam;
            switch ((int)wParam)
            {
                case WM_LBUTTONDOWN:
                    _detector.OnButtonDown(data->Pt.X, data->Pt.Y);
                    break;

                case WM_MOUSEMOVE:
                    if (_detector.OnMove(data->Pt.X, data->Pt.Y, IsCtrlDown) == GestureEvent.Started)
                    {
                        PostMessageW(Overlay.Disc, Overlay.WmGestureStarted, 0, 0);
                    }

                    break;

                case WM_LBUTTONUP:
                    if (_detector.OnButtonUp() == GestureEvent.Released)
                    {
                        PostMessageW(Overlay.Disc, Overlay.WmGestureReleased, 0, 0);
                    }

                    break;

                case WM_RBUTTONDOWN:
                    // ADR-025: botão direito fora do Pizza durante a escolha fecha o menu antes de o Windows
                    // entregar o clique, que segue para a janela de baixo como de costume.
                    Overlay.OnRightButtonDown(data->Pt);
                    break;

                case WM_MOUSEWHEEL when Overlay.IsVisible:
                    // Com o menu aberto, a roda é só nossa: a janela de baixo não rola.
                    PostMessageW(Overlay.Disc, Overlay.WmWheel, 0, (short)(data->MouseData >> 16));
                    return 1;
            }
        }

        return CallNextHookEx(0, code, wParam, lParam);
    }
}
