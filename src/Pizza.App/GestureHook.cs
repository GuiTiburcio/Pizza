using System.Runtime.InteropServices;
using Pizza.Core.Gesture;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// Hooks globais de mouse e teclado. Só detectam e avisam o overlay por mensagem: o Windows
/// desliga hooks lentos, então nada pesado roda aqui dentro.
/// </summary>
internal static unsafe class GestureHook
{
    private static DragGestureDetector _detector = null!;
    private static nint _mouseHook;
    private static nint _keyboardHook;

    public static void Install()
    {
        _detector = new DragGestureDetector(GetSystemMetrics(SM_CXDRAG), GetSystemMetrics(SM_CYDRAG));
        var module = GetModuleHandleW(null);
        _mouseHook = SetWindowsHookExW(WH_MOUSE_LL, &MouseProc, module, 0);
        _keyboardHook = SetWindowsHookExW(WH_KEYBOARD_LL, &KeyboardProc, module, 0);
        if (_mouseHook == 0 || _keyboardHook == 0)
        {
            throw new InvalidOperationException("Não foi possível instalar os hooks de mouse e teclado.");
        }
    }

    public static void Uninstall()
    {
        UnhookWindowsHookEx(_mouseHook);
        UnhookWindowsHookEx(_keyboardHook);
    }

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
                    var ctrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                    if (_detector.OnMove(data->Pt.X, data->Pt.Y, ctrlDown) == GestureEvent.Started)
                    {
                        Overlay.GestureX = data->Pt.X;
                        Overlay.GestureY = data->Pt.Y;
                        PostMessageW(Overlay.Disc, Overlay.WmGestureStarted, 0, 0);
                    }

                    break;

                case WM_LBUTTONUP:
                    if (_detector.OnButtonUp() == GestureEvent.Released)
                    {
                        PostMessageW(Overlay.Disc, Overlay.WmGestureReleased, 0, 0);
                    }

                    break;

                case WM_MOUSEWHEEL when Overlay.IsVisible:
                    // Com o menu aberto, a roda é só nossa: a janela de baixo não rola.
                    PostMessageW(Overlay.Disc, Overlay.WmWheel, 0, (short)(data->MouseData >> 16));
                    return 1;
            }
        }

        return CallNextHookEx(0, code, wParam, lParam);
    }

    [UnmanagedCallersOnly]
    private static nint KeyboardProc(int code, nuint wParam, nint lParam)
    {
        if (code >= 0 && (int)wParam is WM_KEYDOWN or WM_SYSKEYDOWN)
        {
            var data = (KBDLLHOOKSTRUCT*)lParam;
            if (data->VkCode == VK_ESCAPE && _detector.OnEscape() == GestureEvent.Cancelled)
            {
                PostMessageW(Overlay.Disc, Overlay.WmGestureCancelled, 0, 0);
            }
        }

        // O Esc segue adiante de propósito: é ele que faz o Explorer cancelar o arraste.
        return CallNextHookEx(0, code, wParam, lParam);
    }
}
