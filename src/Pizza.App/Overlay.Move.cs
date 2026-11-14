using Pizza.App.Interop;
using Pizza.Core.Gesture;
using Pizza.Core.Visual;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// ADR-037: na escolha por clique, segurar o vidro fora dos alvos move o disco e puxar a borda do círculo muda
/// o tamanho de todos os menus; Ctrl + roda também. A barra de abas e a faixa ficam numa janela própria, que segue
/// o disco com atraso suave e pode ser arrastada para outro lugar. Durante o Ctrl + arrastar de um arquivo nada
/// disso vale: o botão está ocupado com o arquivo e o drop nunca vira MOVE (SEC-05).
/// </summary>
internal static unsafe partial class Overlay
{
    private const nuint TimerBar = 9; // 8 é o da faixa (TimerStrip).
    private const uint BarFrameMs = 10;

    private static readonly DropTarget BarTarget = new(acceptsDrop: true);

    private static GripZone _grip = GripZone.Target; // Target: nenhum arraste do próprio menu em curso.
    private static bool _gripOnBar;
    private static POINT _gripStart;
    private static (int Left, int Top) _gripOrigin;
    private static (double X, double Y) _gripBar;
    private static (int X, int Y) _gripCenter;
    private static double _gripScale;
    private static double _gripDistance;
    private static long _lastBarPress;
    private static bool _rimHover;
    private static RECT _work;

    /// <summary>Janela da barra de abas e da faixa de níveis.</summary>
    private static nint _bar;
    private static double _barX;
    private static double _barY;
    private static long _barFrame;
    private static bool _barLive;

    private static int BarLeft => (int)Math.Round(_barX);

    private static int BarTop => (int)Math.Round(_barY);

    private static void CreateBar(nint instance, uint exStyle)
    {
        _bar = CreateWindowExW(exStyle, ClassName, "Pizza Abas", WS_POPUP, 0, 0, 1, 1, 0, 0, instance, 0);
        BarTarget.Register(_bar);
    }

    /// <summary>Lugar da barra quando parada: o padrão do WF-1 mais o deslocamento que a pessoa escolheu.</summary>
    private static (double X, double Y) BarGoal() =>
        (_left + (MenuSettings.Prefs.Bar.X * _scale), _top + (MenuSettings.Prefs.Bar.Y * _scale));

    private static (int Width, int Height) BarSize() =>
        ((int)Math.Round(DiscLayout.Width * _scale), (int)Math.Round(DiscLayout.DiscTop * _scale));

    /// <summary>Barra no lugar, com o vidro copiado ali (ADR-023). Chamado ao preparar e ao abrir o menu.</summary>
    private static void PlaceBar()
    {
        KillTimer(Disc, TimerBar);
        (_barX, _barY) = BarGoal();
        _barLive = false;
        var (width, height) = BarSize();
        DiscRenderer.PrepareTop(BarLeft, BarTop, width, height, (int)Math.Round(_scale * 96));
    }

    /// <summary>O disco mudou de lugar: a barra vai atrás, translúcida enquanto anda.</summary>
    private static void FollowBar()
    {
        if (_motion == MotionStyle.Instant)
        {
            PlaceBar();
            Render();
            return;
        }

        if (!_barLive)
        {
            _barLive = true;
            var (width, height) = BarSize();
            DiscRenderer.PrepareTop(BarLeft, BarTop, width, height, (int)Math.Round(_scale * 96), live: true);
        }

        if (_barFrame == 0)
        {
            _barFrame = System.Diagnostics.Stopwatch.GetTimestamp();
            SetTimer(Disc, TimerBar, BarFrameMs, 0);
        }
    }

    private static void BarFrame()
    {
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_barFrame, now).TotalMilliseconds;
        _barFrame = now;
        var (goalX, goalY) = BarGoal();
        _barX = BarFollow.Step(_barX, goalX, elapsed);
        _barY = BarFollow.Step(_barY, goalY, elapsed);
        if (_barX == goalX && _barY == goalY && _grip == GripZone.Target)
        {
            KillTimer(Disc, TimerBar);
            _barFrame = 0;
            PlaceBar();
        }

        Render();
    }

    /// <summary>Zona sob o cursor. Qualquer fatia conta como alvo, mesmo apagada, e o nível da faixa também.</summary>
    private static GripZone ZoneAt(nint hwnd, int x, int y)
    {
        if (hwnd == _bar)
        {
            return MenuGrip.Bar(TargetLevel(x, y) is not null);
        }

        // Usa a mesma geometria do desenho, inclusive a extensão do hover sobre o começo da borda.
        var onSlice = _files is { } file && SliceUnder(file, x, y).Converter is not null;
        return onSlice ? GripZone.Target : MenuGrip.Disc((x - _left) / _scale, (y - _top) / _scale, false);
    }

    private static bool BeginGrip(nint hwnd)
    {
        var cursor = Cursor();
        var zone = ZoneAt(hwnd, cursor.X, cursor.Y);
        if (zone == GripZone.Target)
        {
            return false;
        }

        _gripOnBar = hwnd == _bar;
        if (_gripOnBar && Environment.TickCount64 - _lastBarPress <= GetDoubleClickTime())
        {
            // Duplo clique na barra: volta ao lugar padrão, acima do disco.
            _lastBarPress = 0;
            MenuSettings.Prefs.Bar = default;
            MenuSettings.Save();
            FollowBar();
            KeepChoosing();
            return true;
        }

        _lastBarPress = _gripOnBar ? Environment.TickCount64 : 0;
        _grip = zone;
        _gripStart = cursor;
        _gripOrigin = (_left, _top);
        _gripBar = MenuSettings.Prefs.Bar;
        _gripCenter = DiscCenter();
        _gripScale = MenuSettings.Scale;
        _gripDistance = Distance(cursor, _gripCenter);
        SetHover(null);
        SetCapture(hwnd);
        if (_gripOnBar)
        {
            FollowBar();
        }
        else
        {
            Reprepare(live: true);
        }

        KeepChoosing();
        return true;
    }

    private static bool DragGrip()
    {
        if (_grip == GripZone.Target)
        {
            return false;
        }

        var cursor = Cursor();
        if (_gripOnBar)
        {
            // A barra acompanha a mão sem atraso; o deslocamento fica em pixels de desenho.
            MenuSettings.Prefs.Bar = (_gripBar.X + ((cursor.X - _gripStart.X) / _scale), _gripBar.Y + ((cursor.Y - _gripStart.Y) / _scale));
            (_barX, _barY) = BarGoal();
            Render();
        }
        else if (_grip == GripZone.Move)
        {
            _left = _gripOrigin.Left + cursor.X - _gripStart.X;
            _top = _gripOrigin.Top + cursor.Y - _gripStart.Y;
            Render();
            FollowBar();
        }
        else
        {
            Rescale(MenuScale.Drag(_gripScale, _gripDistance, Distance(cursor, _gripCenter)), _gripCenter, live: true);
        }

        KeepChoosing();
        return true;
    }

    /// <summary>Soltou: o vidro copia e desfoca o trecho novo uma vez (ADR-023) e o tamanho fica lembrado.</summary>
    private static bool EndGrip()
    {
        if (_grip == GripZone.Target)
        {
            return false;
        }

        var (resized, onBar) = (_grip == GripZone.Resize, _gripOnBar);
        _grip = GripZone.Target;
        _gripOnBar = false;
        ReleaseCapture();
        if (IsVisible)
        {
            if (!onBar)
            {
                Reprepare(live: false);
            }

            // A barra termina de chegar sozinha e recopia o vidro quando parar.
            FollowBar();
        }

        if (resized || onBar)
        {
            MenuSettings.Save();
        }

        return true;
    }

    /// <summary>Ctrl + roda na escolha: 5% por entalhe. O Ctrl é lido no próprio evento, sem hook de teclado (SEC-01).</summary>
    private static bool WheelResize(int delta)
    {
        if (Session.State != GestureState.Choosing || _grip != GripZone.Target || GetKeyState(VK_CONTROL) >= 0)
        {
            return false;
        }

        Rescale(MenuScale.Step(MenuSettings.Scale, delta), DiscCenter(), live: false);
        MenuSettings.Save();
        KeepChoosing();
        return true;
    }

    /// <summary>Cursor e destaque da borda conforme a zona; só na escolha por clique.</summary>
    private static bool UpdateGripCursor(nint hwnd)
    {
        if (Session.State != GestureState.Choosing)
        {
            return false;
        }

        var cursor = Cursor();
        var zone = _grip != GripZone.Target ? _grip : ZoneAt(hwnd, cursor.X, cursor.Y);
        var (centerX, centerY) = DiscCenter();
        SetCursor(LoadCursorW(0, zone switch
        {
            // A seta aponta para fora do centro, onde quer que o cursor esteja na borda.
            GripZone.Resize => MenuGrip.Direction(cursor.X - centerX, cursor.Y - centerY) switch
            {
                ResizeDirection.Horizontal => IDC_SIZEWE,
                ResizeDirection.Vertical => IDC_SIZENS,
                ResizeDirection.Diagonal => IDC_SIZENWSE,
                _ => IDC_SIZENESW,
            },
            GripZone.Move => IDC_SIZEALL,
            _ => IDC_ARROW,
        }));

        var rim = zone == GripZone.Resize;
        if (rim != _rimHover)
        {
            _rimHover = rim;
            Render();
        }

        return true;
    }

    private static void ResetGrip()
    {
        if (_grip != GripZone.Target)
        {
            _grip = GripZone.Target;
            ReleaseCapture();
        }

        _gripOnBar = false;
        _rimHover = false;
        _barFrame = 0;
        _barLive = false;
        KillTimer(Disc, TimerBar);
    }

    /// <summary>Novo fator para todos os menus, com o centro do disco parado; a barra vai junto, no novo tamanho.</summary>
    private static void Rescale(double user, (int X, int Y) center, bool live)
    {
        MenuSettings.Scale = user;
        var dpi = MenuSettings.Dpi(_work);
        _scale = dpi / 96.0;
        _left = center.X - (int)Math.Round(DiscLayout.CenterX * _scale);
        _top = center.Y - (int)Math.Round(DiscLayout.CenterY * _scale);
        (_barX, _barY) = BarGoal();
        var (width, height) = BarSize();
        _barLive = live;
        DiscRenderer.PrepareTop(BarLeft, BarTop, width, height, dpi, live);
        Reprepare(live);
    }

    private static void Reprepare(bool live)
    {
        var (width, height) = DiscSize();
        DiscRenderer.Prepare(_left, _top, width, height, (int)Math.Round(_scale * 96), live);
        Render();
    }

    private static POINT Cursor()
    {
        POINT cursor;
        GetCursorPos(&cursor);
        return cursor;
    }

    private static double Distance(POINT point, (int X, int Y) center) =>
        Math.Sqrt(Math.Pow(point.X - center.X, 2) + Math.Pow(point.Y - center.Y, 2));
}
