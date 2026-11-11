using System.Diagnostics;
using Pizza.Core.Conversions;
using Pizza.Core.Diagnostics;
using Pizza.Core.Gesture;
using Pizza.Core.Ipc;
using Pizza.Core.Notifications;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// Notificações em janela própria no canto inferior direito (ADR-018, WF-4). Nunca tomam o foco.
/// Vidro translúcido com transparência por pixel, nos dois temas (ADR-028).
/// </summary>
internal static unsafe class Notifications
{
    private const string ClassName = "PizzaNotification";
    private const uint WmConversionDone = WM_APP + 1;
    private const nuint TimerFrame = 1;
    private const nuint TimerHeld = 2;
    private const uint FrameMs = 33;
    private const uint HeldCheckMs = 5000;

    private const int OverflowId = 0;
    private const double SweepMs = 1200;

    public static readonly uint White = Rgb(255, 255, 255);
    public static readonly uint Ink = Rgb(15, 19, 32);
    public static readonly uint Body = Rgb(58, 67, 87);
    public static readonly uint Line = Rgb(221, 225, 234);
    public static readonly uint Soft = Rgb(236, 239, 244);
    public static readonly uint Accent = Rgb(47, 107, 255);
    public static readonly uint Failure = Rgb(196, 60, 60);

    private static readonly NotificationStack Stack = new();
    private static readonly List<Card> Cards = [];

    private static nint _host;
    private static nint _instance;
    private static long _nextId;
    private static long _lastFrame;
    private static double _sweep;
    private static bool _locked;
    private static bool _tracking;
    private static bool _clockRunning;
    private static int _dpi = 96;
    private static RECT _area;

    public static ConversionHistory History { get; } = new() { InUse = Stack.Contains };

    public static int Dpi => _dpi;

    public static RECT WorkArea => _area;

    public static void Create()
    {
        _instance = GetModuleHandleW(null);
        fixed (char* name = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                CbSize = (uint)sizeof(WNDCLASSEXW),
                LpfnWndProc = &WndProc,
                HInstance = _instance,
                HCursor = LoadCursorW(0, IDC_ARROW),
                LpszClassName = name,
            };
            RegisterClassExW(&wc);
        }

        _host = CreateWindowExW(WS_EX_TOOLWINDOW, ClassName, "Pizza Avisos", WS_POPUP, 0, 0, 0, 0, 0, 0, _instance, 0);
        WTSRegisterSessionNotification(_host, NOTIFY_FOR_THIS_SESSION); // PRIV-18.
    }

    /// <summary>Inicia uma conversão. O disco já fechou: daqui em diante, só o canto da tela.</summary>
    public static void Convert(string path, ConverterInfo converter, IReadOnlyDictionary<string, string> options)
    {
        var id = ++_nextId;
        History.Add(id, path, converter, options);
        Stack.Set(id, NotificationKind.Progress, "Convertendo…", $"{Path.GetFileName(path)} → {converter.Label}");
        Conversion.Start(id, path, converter, options, _host, WmConversionDone);
        RefreshHeld();
        Sync();
        FilesPanel.Refresh();
    }

    private static void OnConversionDone()
    {
        if (Conversion.TakeResult() is not { } finished)
        {
            return;
        }

        var result = finished.Result;
        if (result.ErrorCode == ConversionRunner.Cancelled)
        {
            Log.Write(LogEvent.ConversionCancelled);
            return;
        }

        Log.Write(result.ErrorCode is null ? LogEvent.ConversionDone : LogEvent.ConversionFailed);
        if (History.Find(finished.Id) is not { } entry)
        {
            return;
        }

        RefreshHeld();
        if (result.ErrorCode is { } code)
        {
            // FR-42 e PRIV-17: o motivo em português e só o nome do arquivo, nunca o caminho.
            var message = ConversionMessages.For(code);
            History.Fail(finished.Id, message);
            Stack.Set(finished.Id, NotificationKind.Error, "Não foi possível converter", $"{Path.GetFileName(entry.SourcePath)} → {entry.Converter.Label} · {message}");
            Play("erro.wav");
        }
        else
        {
            History.Complete(finished.Id, Path.Combine(Path.GetDirectoryName(entry.SourcePath)!, result.OutputName!));
            Stack.Set(finished.Id, NotificationKind.Success, "Conversão concluída", result.OutputName!);
            Play("concluido.wav");
        }

        Sync();
        FilesPanel.Refresh();
    }

    /// <summary>FR-43: tocado pelo Windows; quem está retido não ouve nada, nem depois.</summary>
    private static void Play(string file)
    {
        if (!Stack.Held)
        {
            PlaySoundW(Path.Combine(AppContext.BaseDirectory, file), 0, SND_FILENAME | SND_ASYNC | SND_NODEFAULT);
        }
    }

    private static void RefreshHeld()
    {
        var quiet = SHQueryUserNotificationState(out var state) == S_OK && state != QUNS_ACCEPTS_NOTIFICATIONS;
        Stack.Held = _locked || quiet;

        // "Não perturbe" não avisa quando começa nem quando termina; só se confere enquanto houver aviso.
        if (!Stack.IsEmpty)
        {
            SetTimer(_host, TimerHeld, HeldCheckMs, 0);
        }
        else
        {
            KillTimer(_host, TimerHeld);
        }
    }

    private static void Sync()
    {
        ReadWorkArea();
        var live = new HashSet<long>();
        var bottom = _area.Bottom - Scale(NotificationCardLayout.Margin);
        var height = Scale(NotificationCardLayout.Height);
        var gap = Scale(NotificationCardLayout.Gap);
        var motion = Overlay.ResolveMotion();
        var visible = Stack.Visible;

        for (var index = 0; index < visible.Count; index++)
        {
            var item = visible[index];
            live.Add(item.Id);
            var top = bottom - ((index + 1) * height) - (index * gap);
            Place(item.Id, top, height, motion, item);
        }

        if (Stack.Overflow > 0)
        {
            live.Add(OverflowId);
            var top = bottom - (visible.Count * (height + gap)) - Scale(NotificationCardLayout.OverflowHeight);
            Place(OverflowId, top, Scale(NotificationCardLayout.OverflowHeight), motion, null);
        }

        foreach (var card in Cards.Where(card => !card.Exiting && !live.Contains(card.Id)).ToArray())
        {
            Exit(card, motion);
        }

        Apply();
        UpdateClock();
    }

    private static void Place(long id, int top, int height, MotionStyle motion, Notification? item)
    {
        var card = Cards.Find(existing => existing.Id == id && !existing.Exiting);
        if (card is null)
        {
            var start = motion == MotionStyle.Slide ? top + height : top;
            card = CreateCard(id, height, start);
            Cards.Add(card);
            Set(card.Alpha, 1, motion);
            Announce(card, item);
        }

        Set(card.Top, top, motion == MotionStyle.Fade ? MotionStyle.Instant : motion);

        var text = item is null ? $"+{Stack.Overflow}" : item.Title + item.Detail + item.Kind;
        if (text != card.Text)
        {
            if (card.Text.Length > 0)
            {
                Announce(card, item);
            }

            card.Text = text;
            card.Item = item;
        }
    }

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

    /// <summary>Sai pelo mesmo caminho por onde entrou.</summary>
    private static void Exit(Card card, MotionStyle motion)
    {
        card.Exiting = true;
        Set(card.Alpha, 0, motion);
        if (motion == MotionStyle.Slide)
        {
            card.Top.Retarget(card.Top.Value + card.Height);
        }
    }

    private static Card CreateCard(long id, int height, int top)
    {
        // Transparência por pixel (ADR-028): o fundo real aparece pelo vidro, e os cantos são desenhados suaves.
        const uint exStyle = WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
        var hwnd = CreateWindowExW(exStyle, ClassName, "Pizza", WS_POPUP, 0, 0, Scale(NotificationCardLayout.Width), height, 0, 0, _instance, 0);
        return new Card(id, hwnd, height, top);
    }
    /// <summary>Leitor de tela (SPECS §2.1): o texto vira o nome da janela e o Windows recebe um alerta.</summary>
    private static void Announce(Card card, Notification? item)
    {
        if (item is not null && (item.Kind != NotificationKind.Progress || card.Text.Length == 0))
        {
            SetWindowTextW(card.Hwnd, $"{item.Title}. {item.Detail}");
            NotifyWinEvent(EVENT_SYSTEM_ALERT, card.Hwnd, OBJID_ALERT, 0);
        }
    }

    private static void Apply()
    {
        var left = _area.Right - Scale(NotificationCardLayout.Margin) - Scale(NotificationCardLayout.Width);
        foreach (var card in Cards.ToArray())
        {
            if (card.Exiting && card.Alpha.IsDone && card.Top.IsDone)
            {
                DestroyWindow(card.Hwnd);
                Cards.Remove(card);
                continue;
            }

            Render(card, left, (int)card.Top.Value);

            // Com o painel "Arquivos..." aberto, os cartões ficam logo abaixo dele, sem cobrir a lista.
            var after = FilesPanel.IsOpen ? FilesPanel.Window : HWND_TOPMOST;
            SetWindowPos(card.Hwnd, after, left, (int)card.Top.Value, Scale(NotificationCardLayout.Width), card.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }

    /// <summary>NFR-02: o temporizador só existe enquanto há algo em movimento ou contando.</summary>
    private static void UpdateClock()
    {
        var needed = Stack.NeedsClock
            || Cards.Exists(card => !card.Alpha.IsDone || !card.Top.IsDone)
            || (Overlay.ResolveMotion() == MotionStyle.Slide && Stack.Visible.Any(item => item.Kind == NotificationKind.Progress));
        if (needed && !_clockRunning)
        {
            _clockRunning = true;
            _lastFrame = Stopwatch.GetTimestamp();
            SetTimer(_host, TimerFrame, FrameMs, 0);
        }
        else if (!needed && _clockRunning)
        {
            _clockRunning = false;
            KillTimer(_host, TimerFrame);
        }
    }

    private static void Frame()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastFrame, now).TotalMilliseconds;
        _lastFrame = now;

        var hadDelayed = Stack.HasDelayed;
        Stack.Advance(elapsed);
        if (hadDelayed && !Stack.HasDelayed)
        {
            RefreshHeld(); // O cartão vai aparecer agora: o "Não perturbe" pode ter sido ligado no último segundo.
        }

        foreach (var card in Cards)
        {
            card.Top.Advance(elapsed);
            card.Alpha.Advance(elapsed);
        }

        _sweep = (_sweep + (elapsed / SweepMs)) % 1;
        Sync();
    }

    /// <summary>Executa um botão, sempre sobre a própria conversão: do cartão ou do painel "Arquivos...".</summary>
    public static void Perform(long id, NotificationAction action)
    {
        if (History.Find(id) is not { } entry)
        {
            return;
        }

        // A janela pode sumir sob o cursor sem avisar que o mouse saiu.
        Stack.Paused = false;
        _tracking = false;
        switch (action)
        {
            case NotificationAction.Cancel:
                Conversion.Cancel(id);
                Stack.Dismiss(id);
                History.Remove(id);
                break;

            case NotificationAction.Open when entry.OutputPath is { } output:
                OpenOutput(entry, output, () => ShellExecuteW(0, "open", output, null, null, SW_SHOWNORMAL));
                break;

            case NotificationAction.ShowInFolder when entry.OutputPath is { } output:
                OpenOutput(entry, output, () => ShowInFolder(output));
                break;

            case NotificationAction.Retry:
                Stack.Dismiss(id);
                History.Remove(id);
                Convert(entry.SourcePath, entry.Converter, entry.Options);
                return;

            case NotificationAction.Details:
                Tray.OpenLogs();
                break;

            case NotificationAction.Close:
                Stack.Dismiss(id);
                break;
        }

        Sync();
        FilesPanel.Refresh();
    }

    /// <summary>Confere só na hora do clique: o arquivo pode ter sido movido ou apagado.</summary>
    private static void OpenOutput(HistoryEntry entry, string output, Action open)
    {
        if (!File.Exists(output))
        {
            entry.Missing = true;
            if (Stack.Contains(entry.Id))
            {
                Stack.Set(entry.Id, NotificationKind.Success, "Arquivo não encontrado", entry.Name);
            }

            return;
        }

        open();
        Stack.Dismiss(entry.Id);
    }

    /// <summary>Pede ao shell para selecionar o arquivo, sem passar o caminho por linha de comando.</summary>
    private static void ShowInFolder(string path)
    {
        if (SHParseDisplayName(path, 0, out var item, 0, out _) == S_OK)
        {
            SHOpenFolderAndSelectItems(item, 0, null, 0);
            CoTaskMemFree(item);
        }
    }

    public static void ReadWorkArea()
    {
        var monitor = MonitorFromPoint(default, MONITOR_DEFAULTTOPRIMARY);
        uint dpiX, dpiY;
        _dpi = GetDpiForMonitor(monitor, 0, &dpiX, &dpiY) == S_OK ? (int)dpiX : 96;
        var info = new MONITORINFO { CbSize = (uint)sizeof(MONITORINFO) };
        GetMonitorInfoW(monitor, &info);
        _area = info.RcWork;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        if (hwnd == _host)
        {
            return HostMessage(hwnd, msg, wParam, lParam);
        }

        var card = Cards.Find(existing => existing.Hwnd == hwnd);
        if (card is null)
        {
            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }

        switch (msg)
        {
            case WM_ERASEBKGND:
                return 1;


            case WM_MOUSEMOVE:
                if (!_tracking)
                {
                    var track = new TRACKMOUSEEVENT { CbSize = (uint)sizeof(TRACKMOUSEEVENT), DwFlags = TME_LEAVE, HwndTrack = hwnd };
                    TrackMouseEvent(&track);
                    _tracking = true;
                    Stack.Paused = true;
                    UpdateClock();
                }

                return 0;

            case WM_MOUSELEAVE:
                _tracking = false;
                Stack.Paused = false;
                UpdateClock();
                return 0;

            case WM_LBUTTONDOWN:
                if (card.Id == OverflowId)
                {
                    FilesPanel.Show();
                }
                else if (card.Item is { } item
                    && NotificationCardLayout.ButtonAt(item.Kind, (short)(lParam & 0xFFFF) * 96 / _dpi, (short)((lParam >> 16) & 0xFFFF) * 96 / _dpi) is { } button)
                {
                    Perform(card.Id, button.Action);
                }

                return 0;

            default:
                return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }

    private static nint HostMessage(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case WmConversionDone:
                OnConversionDone();
                return 0;

            case WM_TIMER when wParam == TimerFrame:
                Frame();
                return 0;

            case WM_TIMER when wParam == TimerHeld:
                RefreshHeld();
                Sync();
                return 0;

            case WM_WTSSESSION_CHANGE:
                if (wParam is WTS_SESSION_LOCK or WTS_SESSION_UNLOCK)
                {
                    _locked = wParam == WTS_SESSION_LOCK;
                    RefreshHeld();
                    Sync();
                }

                return 0;

            default:
                return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }

    private static void Render(Card card, int left, int top)
    {
        var frame = card.Item is { } item
            ? new CardFrame(item.Kind, item.Title, item.Detail, 0, _sweep, Overlay.ResolveMotion() == MotionStyle.Slide)
            : new CardFrame(null, string.Empty, string.Empty, Stack.Overflow, 0, false);
        DiscRenderer.ShowCard(card.Hwnd, left, top, Scale(NotificationCardLayout.Width), card.Height, _dpi, frame, DiscRenderer.DarkTheme(), card.Alpha.Value);
    }
    public static void Round(nint hdc, RECT rect, int radius, uint color)
    {
        var brush = CreateSolidBrush(color);
        var oldBrush = SelectObject(hdc, brush);
        var oldPen = SelectObject(hdc, GetStockObject(NULL_PEN));
        RoundRect(hdc, rect.Left, rect.Top, rect.Right + 1, rect.Bottom + 1, radius * 2, radius * 2);
        SelectObject(hdc, oldPen);
        SelectObject(hdc, oldBrush);
        DeleteObject(brush);
    }

    public static void Label(nint hdc, string text, RECT rect, int fontSize, int weight, uint color, uint format)
    {
        var font = CreateFontW(-Scale(fontSize), 0, 0, 0, weight, 0, 0, 0, 1, 0, 0, CLEARTYPE_QUALITY, 0, "Segoe UI");
        var old = SelectObject(hdc, font);
        SetTextColor(hdc, color);
        DrawTextW(hdc, text, -1, &rect, format);
        SelectObject(hdc, old);
        DeleteObject(font);
    }

    public static int Scale(int value) => value * _dpi / 96;

    private sealed class Card(long id, nint hwnd, int height, int top)
    {
        public long Id { get; } = id;

        public nint Hwnd { get; } = hwnd;

        public int Height { get; } = height;

        public CardTransition Top { get; } = new(top);

        public CardTransition Alpha { get; } = new(0);

        public bool Exiting { get; set; }

        public string Text { get; set; } = string.Empty;

        public Notification? Item { get; set; }
    }
}
