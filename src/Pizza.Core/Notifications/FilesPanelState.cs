using Pizza.Core.Conversions;

namespace Pizza.Core.Notifications;

/// <summary>Rolagem, foco e geometria do painel "Arquivos..." (FR-44). Medidas em pixels a 96 dpi.</summary>
public sealed class FilesPanelState(ConversionHistory history)
{
    public const int Width = NotificationCardLayout.Width;
    public const int HeaderHeight = 48;
    public const int RowHeight = 108;
    public const int EmptyHeight = 80;

    private const int MaxRows = 4;
    private const int Margin = 16;
    private const int Gap = 8;
    private const int ButtonTop = 56;
    private const int ButtonHeight = 44;
    private const int WheelNotch = 120;

    private static readonly int HalfWidth = (Width - (2 * Margin) - Gap) / 2;
    private static readonly PanelRect Left = new(Margin, ButtonTop, Margin + HalfWidth, ButtonTop + ButtonHeight);
    private static readonly PanelRect Right = new(Margin + HalfWidth + Gap, ButtonTop, Width - Margin, ButtonTop + ButtonHeight);

    private static readonly NotificationButton[] DoneButtons =
    [
        new(NotificationAction.Open, "Abrir", Left),
        new(NotificationAction.ShowInFolder, "Mostrar na pasta", Right),
    ];

    private static readonly NotificationButton[] FailedButtons = [new(NotificationAction.Retry, "Tentar de novo", Left)];

    private int _focus;

    public int Scroll { get; private set; }

    public int Height => HeaderHeight + ViewportHeight;

    public (int Row, NotificationAction Action)? Focused
    {
        get
        {
            var stops = Stops();
            return stops.Count == 0 ? null : (stops[_focus].Row, stops[_focus].Button.Action);
        }
    }

    public int ViewportHeight => history.Items.Count == 0 ? EmptyHeight : Math.Min(history.Items.Count, MaxRows) * RowHeight;

    public int ContentHeight => history.Items.Count * RowHeight;

    public int MaxScroll => Math.Max(0, ContentHeight - ViewportHeight);

    public static IReadOnlyList<NotificationButton> Buttons(HistoryState state) => state switch
    {
        HistoryState.Done => DoneButtons,
        HistoryState.Failed => FailedButtons,
        _ => [],
    };

    /// <summary>A lista mudou: mantém foco e rolagem dentro do que ainda existe.</summary>
    public void Refresh()
    {
        _focus = Math.Clamp(_focus, 0, Math.Max(0, Stops().Count - 1));
        Scroll = Math.Clamp(Scroll, 0, MaxScroll);
    }

    public void Wheel(int delta) => Scroll = Math.Clamp(Scroll - (delta * RowHeight / WheelNotch / 2), 0, MaxScroll);

    public (long Id, NotificationAction Action)? Press(PanelKey key)
    {
        var stops = Stops();
        if (stops.Count == 0)
        {
            return null;
        }

        switch (key)
        {
            case PanelKey.Tab or PanelKey.Down:
                MoveFocus(1, stops);
                return null;

            case PanelKey.ShiftTab or PanelKey.Up:
                MoveFocus(-1, stops);
                return null;

            case PanelKey.Enter or PanelKey.Space:
                return (history.Items[stops[_focus].Row].Id, stops[_focus].Button.Action);

            default:
                return null;
        }
    }

    public (long Id, NotificationAction Action)? Click(int x, int y)
    {
        if (y < HeaderHeight)
        {
            return null;
        }

        var content = y - HeaderHeight + Scroll;
        var row = content / RowHeight;
        if (row >= history.Items.Count)
        {
            return null;
        }

        var stops = Stops();
        for (var index = 0; index < stops.Count; index++)
        {
            if (stops[index].Row == row && stops[index].Button.Bounds.Contains(x, content % RowHeight))
            {
                _focus = index;
                return (history.Items[row].Id, stops[index].Button.Action);
            }
        }

        return null;
    }

    private void MoveFocus(int step, List<(int Row, NotificationButton Button)> stops)
    {
        _focus = (_focus + step + stops.Count) % stops.Count;
        var top = stops[_focus].Row * RowHeight;
        if (top < Scroll)
        {
            Scroll = top;
        }
        else if (top + RowHeight > Scroll + ViewportHeight)
        {
            Scroll = top + RowHeight - ViewportHeight;
        }
    }

    private List<(int Row, NotificationButton Button)> Stops()
    {
        var stops = new List<(int, NotificationButton)>();
        for (var row = 0; row < history.Items.Count; row++)
        {
            foreach (var button in Buttons(history.Items[row].State))
            {
                stops.Add((row, button));
            }
        }

        return stops;
    }
}
