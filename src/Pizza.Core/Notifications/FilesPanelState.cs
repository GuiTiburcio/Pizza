using Pizza.Core.Conversions;

namespace Pizza.Core.Notifications;

/// <summary>
/// Rolagem, foco e geometria do painel "Arquivos..." (FR-44). Medidas em pixels a 96 dpi.
/// Prancha 4c-3: linhas compactas; só a linha do botão em foco fica aberta e mostra os botões.
/// </summary>
public sealed class FilesPanelState(ConversionHistory history)
{
    public const int Width = NotificationCardLayout.Width;
    public const int HeaderHeight = 44;
    public const int RowHeight = 52;
    public const int OpenHeight = 108;
    public const int EmptyHeight = 80;
    public const int BottomPadding = 8;

    private const int MaxRows = 5;
    private const int Margin = 20;
    private const int Gap = 8;
    private const int ButtonTop = RowHeight;
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

    public int Height => HeaderHeight + ViewportHeight + BottomPadding;

    public (int Row, NotificationAction Action)? Focused
    {
        get
        {
            var stops = Stops();
            return stops.Count == 0 ? null : (stops[_focus].Row, stops[_focus].Button.Action);
        }
    }

    /// <summary>A linha aberta é a do botão em foco; -1 quando nenhuma linha tem botões.</summary>
    public int OpenRow => Focused?.Row ?? -1;

    /// <summary>Com a linha aberta, a janela não muda de altura quando o foco anda.</summary>
    public int ViewportHeight => history.Items.Count == 0 ? EmptyHeight : Math.Min(ContentHeight, (MaxRows * RowHeight) + OpenExtra);

    public int ContentHeight => (history.Items.Count * RowHeight) + OpenExtra;

    public int MaxScroll => Math.Max(0, ContentHeight - ViewportHeight);

    private int OpenExtra => OpenRow < 0 ? 0 : OpenHeight - RowHeight;

    public static IReadOnlyList<NotificationButton> Buttons(HistoryState state) => state switch
    {
        HistoryState.Done => DoneButtons,
        HistoryState.Failed => FailedButtons,
        _ => [],
    };

    public static string Status(HistoryEntry entry) => entry.State switch
    {
        HistoryState.Running => "Convertendo…",
        HistoryState.Failed => $"Erro · {entry.Message}",
        _ => entry.Missing ? "Arquivo não encontrado" : $"Concluído · {entry.Converter.Label}",
    };

    /// <summary>Topo da linha na lista, sem a rolagem.</summary>
    public int RowTop(int row)
    {
        var open = OpenRow;
        return (row * RowHeight) + (open >= 0 && row > open ? OpenHeight - RowHeight : 0);
    }

    public int RowHeightAt(int row) => row == OpenRow ? OpenHeight : RowHeight;

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
                Focus((_focus + 1) % stops.Count);
                return null;

            case PanelKey.ShiftTab or PanelKey.Up:
                Focus((_focus - 1 + stops.Count) % stops.Count);
                return null;

            case PanelKey.Enter or PanelKey.Space:
                return (history.Items[stops[_focus].Row].Id, stops[_focus].Button.Action);

            default:
                return null;
        }
    }

    /// <summary>No botão da linha aberta, executa; numa linha fechada com botões, só a abre.</summary>
    public (long Id, NotificationAction Action)? Click(int x, int y)
    {
        if (y < HeaderHeight)
        {
            return null;
        }

        var content = y - HeaderHeight + Scroll;
        var row = RowAt(content);
        if (row < 0)
        {
            return null;
        }

        var stops = Stops();
        var first = stops.FindIndex(stop => stop.Row == row);
        if (first < 0)
        {
            return null;
        }

        if (row != OpenRow)
        {
            Focus(first);
            return null;
        }

        var inside = content - RowTop(row);
        for (var index = first; index < stops.Count && stops[index].Row == row; index++)
        {
            if (stops[index].Button.Bounds.Contains(x, inside))
            {
                _focus = index;
                return (history.Items[row].Id, stops[index].Button.Action);
            }
        }

        return null;
    }

    private int RowAt(int content)
    {
        for (var row = 0; row < history.Items.Count; row++)
        {
            if (content < RowTop(row) + RowHeightAt(row))
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>Move o foco e rola até a linha aberta aparecer inteira.</summary>
    private void Focus(int index)
    {
        _focus = index;
        var row = OpenRow;
        var top = RowTop(row);
        if (top < Scroll)
        {
            Scroll = top;
        }
        else if (top + OpenHeight > Scroll + ViewportHeight)
        {
            Scroll = top + OpenHeight - ViewportHeight;
        }

        Scroll = Math.Clamp(Scroll, 0, MaxScroll);
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
