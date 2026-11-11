using Pizza.Core.Conversions;

namespace Pizza.Core.Notifications;

public enum NotificationAction
{
    Cancel,
    Open,
    ShowInFolder,
    Retry,
    Details,
    Close,
    Pause,
    Resume,
}

public readonly record struct NotificationButton(NotificationAction Action, string Label, PanelRect Bounds);

/// <summary>Geometria do cartão (WF-4), em pixels a 96 dpi. Botões de 44 px, como no sistema visual.</summary>
public static class NotificationCardLayout
{
    public const int Width = 380;
    public const int Height = 148;
    public const int OverflowHeight = 44;
    public const int Margin = 16;
    public const int Gap = 8;
    public const int ProgressTextTop = 76;
    public const int ProgressBarTop = 100;

    /// <summary>ADR-029: continuar, pausar e parar, ao lado do título; o desenho tem 32 px dentro do alvo de 44 px.</summary>
    public const int ControlSize = 44;

    private const int ButtonTop = 88;
    private const int ButtonHeight = 44;
    private const int CloseSize = 32;

    private static readonly int HalfWidth = (Width - (2 * Margin) - Gap) / 2;
    private static readonly PanelRect Left = new(Margin, ButtonTop, Margin + HalfWidth, ButtonTop + ButtonHeight);
    private static readonly PanelRect Right = new(Margin + HalfWidth + Gap, ButtonTop, Width - Margin, ButtonTop + ButtonHeight);
    private static readonly PanelRect CloseBox = new(Width - 8 - CloseSize, 8, Width - 8, 8 + CloseSize);

    private static readonly NotificationButton[] ProgressButtons =
    [
        new(NotificationAction.Resume, "Continuar", Control(2)),
        new(NotificationAction.Pause, "Pausar", Control(1)),
        new(NotificationAction.Cancel, "Parar", Control(0)),
    ];

    private static readonly NotificationButton[] SuccessButtons =
    [
        new(NotificationAction.Open, "Abrir", Left),
        new(NotificationAction.ShowInFolder, "Mostrar na pasta", Right),
        new(NotificationAction.Close, "×", CloseBox),
    ];

    private static readonly NotificationButton[] ErrorButtons =
    [
        new(NotificationAction.Retry, "Tentar de novo", Left),
        new(NotificationAction.Details, "Ver detalhes", Right),
        new(NotificationAction.Close, "×", CloseBox),
    ];

    public static IReadOnlyList<NotificationButton> Buttons(NotificationKind kind) => kind switch
    {
        NotificationKind.Progress => ProgressButtons,
        NotificationKind.Success => SuccessButtons,
        _ => ErrorButtons,
    };

    /// <summary>Alvo do controle na posição dada, da direita para a esquerda.</summary>
    private static PanelRect Control(int fromRight)
    {
        var right = Width - Margin - (fromRight * (ControlSize + 4));
        return new PanelRect(right - ControlSize, 6, right, 6 + ControlSize);
    }

    public static NotificationButton? ButtonAt(NotificationKind kind, int x, int y)
    {
        foreach (var button in Buttons(kind))
        {
            if (button.Bounds.Contains(x, y))
            {
                return button;
            }
        }

        return null;
    }
}
