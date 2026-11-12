using Pizza.Core.Conversions;

namespace Pizza.Core.Notifications;

public enum TrayCommand
{
    Close,
    Files,
    OpenLogs,
    Quit,
}

public readonly record struct TrayItem(TrayCommand Command, string Label, PanelRect Bounds);

/// <summary>
/// Menu da bandeja em vidro (ADR-020, prancha 4d-2): foco, cliques e geometria. Medidas em pixels a 96 dpi.
/// Configuração (Fase 8) e Play, Pause e Stop da fila (v1.x) só entram quando funcionarem.
/// </summary>
public sealed class TrayMenuState
{
    public const int Width = 270;
    public const int Padding = 8;
    public const int HeaderTop = Padding;
    public const int HeaderHeight = 40;
    public const int SeparatorHeight = 9;

    private const int ItemHeight = 44;
    private const int ItemGap = 2;

    private int _focus;

    public TrayMenuState()
    {
        var top = HeaderTop + HeaderHeight + SeparatorHeight;
        TrayItem Add(TrayCommand command, string label)
        {
            var item = new TrayItem(command, label, new PanelRect(Padding, top, Width - Padding, top + ItemHeight));
            top += ItemHeight + ItemGap;
            return item;
        }

        var files = Add(TrayCommand.Files, "Arquivos…");
        var logs = Add(TrayCommand.OpenLogs, "Abrir pasta de logs");
        top += SeparatorHeight - ItemGap;
        SecondSeparatorTop = top - SeparatorHeight;
        var quit = Add(TrayCommand.Quit, "Sair");
        Items = [files, logs, quit];
        Height = quit.Bounds.Bottom + Padding;
    }

    public IReadOnlyList<TrayItem> Items { get; }

    public int Height { get; }

    /// <summary>Topo do separador antes de "Sair".</summary>
    public int SecondSeparatorTop { get; }

    public int Focus => _focus;

    public TrayCommand? Press(PanelKey key)
    {
        switch (key)
        {
            case PanelKey.Escape:
                return TrayCommand.Close;

            case PanelKey.Enter or PanelKey.Space:
                return Items[_focus].Command;

            case PanelKey.Down or PanelKey.Tab:
                _focus = (_focus + 1) % Items.Count;
                return null;

            case PanelKey.Up or PanelKey.ShiftTab:
                _focus = (_focus + Items.Count - 1) % Items.Count;
                return null;

            default:
                return null;
        }
    }

    /// <summary>O foco segue o mouse; devolve se mudou, para redesenhar só quando precisa.</summary>
    public bool Hover(int x, int y)
    {
        if (ItemAt(x, y) is not { } index || index == _focus)
        {
            return false;
        }

        _focus = index;
        return true;
    }

    public TrayCommand? Click(int x, int y) => ItemAt(x, y) is { } index ? Items[index].Command : null;

    /// <summary>Acima e à esquerda do cursor, como perto do relógio; vira para o outro lado se não couber.</summary>
    public static (int Left, int Top) Place(int x, int y, int width, int height, PanelRect area)
    {
        var left = x - width >= area.Left ? x - width : x;
        var top = y - height >= area.Top ? y - height : y;
        return (Math.Clamp(left, area.Left, area.Right - width), Math.Clamp(top, area.Top, area.Bottom - height));
    }

    private int? ItemAt(int x, int y)
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index].Bounds.Contains(x, y))
            {
                return index;
            }
        }

        return null;
    }
}
