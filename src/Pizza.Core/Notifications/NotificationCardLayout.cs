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
    public const int ProgressBarTop = 104;

    private const int ButtonTop = 88;
    private const int ButtonHeight = 44;
    private const int CancelWidth = 120;
    private const int CloseSize = 32;

    private static readonly int HalfWidth = (Width - (2 * Margin) - Gap) / 2;
    private static readonly PanelRect Left = new(Margin, ButtonTop, Margin + HalfWidth, ButtonTop + ButtonHeight);
    private static readonly PanelRect Right = new(Margin + HalfWidth + Gap, ButtonTop, Width - Margin, ButtonTop + ButtonHeight);
    private static readonly PanelRect CloseBox = new(Width - 8 - CloseSize, 8, Width - 8, 8 + CloseSize);
    private static readonly PanelRect CancelBox = new(Width - Margin - CancelWidth, ButtonTop, Width - Margin, ButtonTop + ButtonHeight);

    private static readonly NotificationButton[] ProgressButtons = [new(NotificationAction.Cancel, "Cancelar", CancelBox)];

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
