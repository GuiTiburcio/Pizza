using System.Runtime.InteropServices;

namespace Pizza.App.Interop;

/// <summary>Chamadas Win32/OLE usadas pelo residente. Tudo via LibraryImport, compatível com NativeAOT.</summary>
internal static unsafe partial class Native
{
    // Mensagens
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_ACTIVATE = 0x0006;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_PAINT = 0x000F;
    public const uint WM_ERASEBKGND = 0x0014;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_TIMER = 0x0113;
    public const uint WM_APP = 0x8000;
    public const int WM_CONTEXTMENU = 0x007B;
    public const int WM_SETCURSOR = 0x0020;
    public const int WM_CAPTURECHANGED = 0x0215;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_MOUSEWHEEL = 0x020A;
    public const int WM_MOUSELEAVE = 0x02A3;
    public const uint WM_WTSSESSION_CHANGE = 0x02B1;
    public const uint TME_LEAVE = 0x2;
    public const uint NOTIFY_FOR_THIS_SESSION = 0;
    public const nuint WTS_SESSION_LOCK = 7;
    public const nuint WTS_SESSION_UNLOCK = 8;
    public const int QUNS_ACCEPTS_NOTIFICATIONS = 5;
    public const uint EVENT_SYSTEM_ALERT = 0x0002;
    public const int OBJID_ALERT = -10;
    public const uint SND_ASYNC = 0x1;
    public const uint SND_NODEFAULT = 0x2;
    public const uint SND_FILENAME = 0x20000;

    // Hooks e teclas
    public const int WH_MOUSE_LL = 14;
    public const int VK_LBUTTON = 0x01;
    public const int VK_RBUTTON = 0x02;
    public const int VK_CONTROL = 0x11;
    public const int VK_TAB = 0x09;
    public const int VK_RETURN = 0x0D;
    public const int VK_SHIFT = 0x10;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_SPACE = 0x20;
    public const int VK_LEFT = 0x25;
    public const int VK_UP = 0x26;
    public const int VK_RIGHT = 0x27;
    public const int VK_DOWN = 0x28;
    public const int VK_BACK = 0x08;
    public const int VK_END = 0x23;
    public const int VK_HOME = 0x24;
    public const int WM_CHAR = 0x0102;

    // Bandeja e menu
    public const uint NIM_ADD = 0;
    public const uint NIM_DELETE = 2;
    public const uint NIM_SETVERSION = 4;
    public const uint NIF_MESSAGE = 0x01;
    public const uint NIF_ICON = 0x02;
    public const uint NIF_TIP = 0x04;
    public const uint NIF_SHOWTIP = 0x80;
    public const uint NOTIFYICON_VERSION_4 = 4;
    public const uint IMAGE_ICON = 1;
    public const int SM_CXSMICON = 49;
    public const int SM_CYSMICON = 50;
    public const int SW_SHOWNORMAL = 1;

    // Janelas
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_EX_TOPMOST = 0x00000008;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_LAYERED = 0x00080000;
    public const uint WS_EX_NOACTIVATE = 0x08000000;
    public const uint LWA_ALPHA = 0x2;
    public const int SW_HIDE = 0;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public static readonly nint HWND_TOPMOST = -1;
    public const int SM_SWAPBUTTON = 23;
    public const int SM_CXDRAG = 68;
    public const int SM_CYDRAG = 69;
    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;
    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;
    public const int IDC_ARROW = 32512;
    public const int IDC_SIZENWSE = 32642; // ADR-037: puxar a borda muda o tamanho.
    public const int IDC_SIZEALL = 32646; // ADR-037: segurar o vidro move a janela.

    // GDI
    public const uint SPI_GETCLIENTAREAANIMATION = 0x1042;
    public const int PS_SOLID = 0;
    public const uint DT_WORDBREAK = 0x10;
    public const uint DT_EDITCONTROL = 0x2000;
    public const uint DT_PATH_ELLIPSIS = 0x4000;

    // OLE
    public const ushort CF_HDROP = 15;
    public const uint DVASPECT_CONTENT = 1;
    public const uint TYMED_HGLOBAL = 1;
    public const int DROPEFFECT_NONE = 0;
    public const int DROPEFFECT_COPY = 1;
    public const int S_OK = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public nint Hwnd;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public POINT Pt;
        public uint LPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WNDCLASSEXW
    {
        public uint CbSize;
        public uint Style;
        public delegate* unmanaged<nint, uint, nuint, nint, nint> LpfnWndProc;
        public int CbClsExtra;
        public int CbWndExtra;
        public nint HInstance;
        public nint HIcon;
        public nint HCursor;
        public nint HbrBackground;
        public char* LpszMenuName;
        public char* LpszClassName;
        public nint HIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PAINTSTRUCT
    {
        public nint Hdc;
        public int FErase;
        public RECT RcPaint;
        public int FRestore;
        public int FIncUpdate;
        public fixed byte RgbReserved[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint CbSize;
        public RECT RcMonitor;
        public RECT RcWork;
        public uint DwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FORMATETC
    {
        public ushort CfFormat;
        public nint Ptd;
        public uint DwAspect;
        public int Lindex;
        public uint Tymed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STGMEDIUM
    {
        public uint Tymed;
        public nint HGlobal;
        public nint PUnkForRelease;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NOTIFYICONDATAW
    {
        public uint CbSize;
        public nint HWnd;
        public uint UID;
        public uint UFlags;
        public uint UCallbackMessage;
        public nint HIcon;
        public fixed char SzTip[128];
        public uint DwState;
        public uint DwStateMask;
        public fixed char SzInfo[256];
        public uint UVersion;
        public fixed char SzInfoTitle[64];
        public uint DwInfoFlags;
        public Guid GuidItem;
        public nint HBalloonIcon;
    }

    public const byte AC_SRC_ALPHA = 1;
    public const uint ULW_ALPHA = 2;
    public static readonly nint HKEY_CURRENT_USER = unchecked((int)0x80000001);
    public const uint RRF_RT_REG_DWORD = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE
    {
        public int Cx;
        public int Cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TRACKMOUSEEVENT
    {
        public uint CbSize;
        public uint DwFlags;
        public nint HwndTrack;
        public uint DwHoverTime;
    }

    /// <summary>COLORREF a partir de RGB.</summary>
    public static uint Rgb(byte r, byte g, byte b) => r | ((uint)g << 8) | ((uint)b << 16);

    // user32
    [LibraryImport("user32.dll")]
    public static partial ushort RegisterClassExW(WNDCLASSEXW* wc);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowExW(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    public static partial nint DefWindowProcW(nint hwnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    public static partial int GetMessageW(MSG* msg, nint hwnd, uint min, uint max);

    [LibraryImport("user32.dll")]
    public static partial int TranslateMessage(MSG* msg);

    [LibraryImport("user32.dll")]
    public static partial nint DispatchMessageW(MSG* msg);

    [LibraryImport("user32.dll")]
    public static partial int PostMessageW(nint hwnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    public static partial int GetCursorPos(POINT* point);

    // dwmapi: espera a próxima composição da tela.
    [LibraryImport("dwmapi.dll")]
    public static partial int DwmFlush();


    [LibraryImport("user32.dll")]
    public static partial void PostQuitMessage(int exitCode);

    [LibraryImport("user32.dll")]
    public static partial int ShowWindow(nint hwnd, int cmd);

    [LibraryImport("user32.dll")]
    public static partial int SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    public static partial int SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, uint flags);

    [LibraryImport("user32.dll")]
    public static partial nint BeginPaint(nint hwnd, PAINTSTRUCT* ps);

    [LibraryImport("user32.dll")]
    public static partial int EndPaint(nint hwnd, PAINTSTRUCT* ps);

    [LibraryImport("user32.dll")]
    public static partial int FillRect(nint hdc, RECT* rect, nint brush);

    [LibraryImport("user32.dll")]
    public static partial nuint SetTimer(nint hwnd, nuint id, uint ms, nint proc);

    [LibraryImport("user32.dll")]
    public static partial int KillTimer(nint hwnd, nuint id);

    [LibraryImport("user32.dll")]
    public static partial nint SetWindowsHookExW(int id, delegate* unmanaged<int, nuint, nint, nint> proc, nint module, uint threadId);

    [LibraryImport("user32.dll")]
    public static partial nint CallNextHookEx(nint hook, int code, nuint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    public static partial int UnhookWindowsHookEx(nint hook);

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int key);

    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    public static partial nint LoadImageW(nint instance, nint name, uint type, int width, int height, uint flags);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessageW(string name);

    [LibraryImport("user32.dll")]
    public static partial int SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hwnd, nint hdc);

    [LibraryImport("user32.dll")]
    public static partial int UpdateLayeredWindow(nint hwnd, nint screen, POINT* position, SIZE* size, nint source, POINT* sourcePosition, uint key, BLENDFUNCTION* blend, uint flags);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateDIBSection(nint hdc, BITMAPINFOHEADER* info, uint usage, nint* bits, nint section, uint offset);

    [LibraryImport("advapi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int RegGetValueW(nint key, string subKey, string value, uint flags, uint* type, void* data, uint* size);

    [LibraryImport("user32.dll")]
    public static partial int DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SetWindowTextW(nint hwnd, string text);

    [LibraryImport("user32.dll")]
    public static partial void NotifyWinEvent(uint eventId, nint hwnd, int objectId, int childId);

    [LibraryImport("user32.dll")]
    public static partial int TrackMouseEvent(TRACKMOUSEEVENT* track);

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    public static partial nint SetFocus(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint hwnd, uint* processId);

    [LibraryImport("user32.dll")]
    public static partial int AttachThreadInput(uint attach, uint attachTo, int doAttach);

    [LibraryImport("user32.dll")]
    public static partial short GetKeyState(int key);

    [LibraryImport("user32.dll")]
    public static partial nint SetCapture(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseCapture();

    [LibraryImport("user32.dll")]
    public static partial nint LoadCursorW(nint instance, nint name);

    [LibraryImport("user32.dll")]
    public static partial nint SetCursor(nint cursor);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromPoint(POINT pt, uint flags);

    [LibraryImport("user32.dll")]
    public static partial int GetMonitorInfoW(nint monitor, MONITORINFO* info);

    [LibraryImport("user32.dll")]
    public static partial int SetProcessDpiAwarenessContext(nint context);

    // gdi32
    [LibraryImport("user32.dll")]
    public static partial int SystemParametersInfoW(uint action, uint param, int* value, uint winIni);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial int BitBlt(nint target, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);

    [LibraryImport("gdi32.dll")]
    public static partial int DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateSolidBrush(uint color);

    [LibraryImport("gdi32.dll")]
    public static partial int DeleteObject(nint obj);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint obj);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateEllipticRgn(int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll")]
    public static partial int Ellipse(nint hdc, int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll")]
    public static partial int Polygon(nint hdc, POINT* points, int count);

    [LibraryImport("gdi32.dll")]
    public static partial int SelectClipRgn(nint hdc, nint region);

    // kernel32
    [LibraryImport("kernel32.dll")]
    public static partial nint GetModuleHandleW(char* name);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    // ole32 / shell32
    [LibraryImport("ole32.dll")]
    public static partial int OleInitialize(nint reserved);

    [LibraryImport("ole32.dll")]
    public static partial void OleUninitialize();

    [LibraryImport("ole32.dll")]
    public static partial int RegisterDragDrop(nint hwnd, nint dropTarget);

    [LibraryImport("ole32.dll")]
    public static partial int RevokeDragDrop(nint hwnd);

    [LibraryImport("ole32.dll")]
    public static partial void ReleaseStgMedium(STGMEDIUM* medium);

    [LibraryImport("shell32.dll")]
    public static partial uint DragQueryFileW(nint hDrop, uint index, char* buffer, uint size);

    [LibraryImport("wtsapi32.dll")]
    public static partial int WTSRegisterSessionNotification(nint hwnd, uint flags);

    [LibraryImport("winmm.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int PlaySoundW(string sound, nint module, uint flags);

    [LibraryImport("ole32.dll")]
    public static partial void CoTaskMemFree(nint memory);

    [LibraryImport("shell32.dll")]
    public static partial int SHQueryUserNotificationState(out int state);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SHParseDisplayName(string name, nint bindContext, out nint item, uint attributesIn, out uint attributesOut);

    [LibraryImport("shell32.dll")]
    public static partial int SHOpenFolderAndSelectItems(nint folder, uint count, nint* items, uint flags);

    [LibraryImport("shell32.dll")]
    public static partial int Shell_NotifyIconW(uint message, NOTIFYICONDATAW* data);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint ShellExecuteW(nint hwnd, string operation, string file, string? parameters, string? directory, int show);
}
