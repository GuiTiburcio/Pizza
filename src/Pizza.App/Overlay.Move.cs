using Pizza.App.Interop;
using Pizza.Core.Gesture;
using Pizza.Core.Visual;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// ADR-037: na escolha por clique, segurar o vidro fora dos alvos move o disco e puxar a borda do círculo muda
/// o tamanho de todos os menus; Ctrl + roda também. Durante o Ctrl + arrastar de um arquivo nada disso vale:
/// o botão está ocupado com o arquivo e o drop nunca vira MOVE (SEC-05).
/// </summary>
internal static unsafe partial class Overlay
{
    private static GripZone _grip = GripZone.Target; // Target: nenhum arraste do próprio menu em curso.
    private static POINT _gripStart;
    private static (int Left, int Top) _gripOrigin;
    private static (int X, int Y) _gripCenter;
    private static double _gripScale;
    private static double _gripDistance;
    private static bool _rimHover;
    private static RECT _work;

    /// <summary>Zona sob o cursor. Qualquer fatia conta como alvo, mesmo apagada, e o nível da faixa também.</summary>
    private static GripZone ZoneAt(int x, int y)
    {
        var (centerX, centerY) = DiscCenter();
        var slot = RadialMenu.SlotAt((x - centerX) / _scale, (y - centerY) / _scale, Rotation(), new double[RadialMenu.SlotCount]);
        var onSlice = slot is { } index && index < CurrentSlices().Count;
        var onTarget = onSlice || TargetLevel(x, y) is not null;
        return MenuGrip.Disc((x - _left) / _scale, (y - _top) / _scale, onTarget);
    }

    private static bool BeginGrip()
    {
        var cursor = Cursor();
        var zone = ZoneAt(cursor.X, cursor.Y);
        if (zone == GripZone.Target)
        {
            return false;
        }

        _grip = zone;
        _gripStart = cursor;
        _gripOrigin = (_left, _top);
        _gripCenter = DiscCenter();
        _gripScale = MenuSettings.Scale;
        _gripDistance = Distance(cursor, _gripCenter);
        SetHover(null);
        SetCapture(Disc);
        Reprepare(live: true);
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
        if (_grip == GripZone.Move)
        {
            _left = _gripOrigin.Left + cursor.X - _gripStart.X;
            _top = _gripOrigin.Top + cursor.Y - _gripStart.Y;
            Render();
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

        var resized = _grip == GripZone.Resize;
        _grip = GripZone.Target;
        ReleaseCapture();
        if (IsVisible)
        {
            Reprepare(live: false);
        }

        if (resized)
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
    private static bool UpdateGripCursor()
    {
        if (Session.State != GestureState.Choosing)
        {
            return false;
        }

        var cursor = Cursor();
        var zone = _grip != GripZone.Target ? _grip : ZoneAt(cursor.X, cursor.Y);
        SetCursor(LoadCursorW(0, zone switch
        {
            GripZone.Resize => IDC_SIZENWSE,
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

        _rimHover = false;
    }

    /// <summary>Novo fator para todos os menus, com o centro do disco parado.</summary>
    private static void Rescale(double user, (int X, int Y) center, bool live)
    {
        MenuSettings.Scale = user;
        var dpi = MenuSettings.Dpi(_work);
        _scale = dpi / 96.0;
        _left = center.X - (int)Math.Round(DiscLayout.CenterX * _scale);
        _top = center.Y - (int)Math.Round(DiscLayout.CenterY * _scale);
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
