namespace Pizza.Core.Notifications;

public enum NotificationKind
{
    Progress,
    Success,
    Error,
}

public sealed class Notification(long id)
{
    public long Id { get; } = id;

    public NotificationKind Kind { get; internal set; }

    public string Title { get; internal set; } = string.Empty;

    public string Detail { get; internal set; } = string.Empty;

    internal double DelayMs { get; set; }

    internal double RemainingMs { get; set; }
}

/// <summary>Quais notificações estão na tela e por quanto tempo (FR-40, FR-42; SPECS §2.1).</summary>
public sealed class NotificationStack
{
    private const int MaxVisible = 3;
    private const double ProgressDelayMs = 1000;
    private const double SuccessMs = 5000;

    private readonly List<Notification> _items = [];

    /// <summary>Mouse sobre um cartão: o tempo para.</summary>
    public bool Paused { get; set; }

    /// <summary>Tela bloqueada ou "Não perturbe": nada aparece e o tempo para (PRIV-18).</summary>
    public bool Held { get; set; }

    /// <summary>As mais novas primeiro: a de índice 0 fica embaixo.</summary>
    public IReadOnlyList<Notification> Visible => Held ? [] : Eligible().Take(MaxVisible).ToArray();

    public int Overflow => Held ? 0 : Math.Max(0, Eligible().Count() - MaxVisible);

    /// <summary>Falso quando nada depende do tempo: em repouso, o residente não usa temporizador (NFR-02).</summary>
    public bool NeedsClock =>
        _items.Exists(item => item.DelayMs > 0) || (!Paused && Visible.Any(item => item.Kind == NotificationKind.Success));

    public bool IsEmpty => _items.Count == 0;

    public bool HasDelayed => _items.Exists(item => item.DelayMs > 0);

    public bool Contains(long id) => _items.Exists(item => item.Id == id);

    public void Set(long id, NotificationKind kind, string title, string detail)
    {
        var item = _items.Find(existing => existing.Id == id);
        if (item is null)
        {
            item = new Notification(id);
            _items.Add(item);
        }

        item.Kind = kind;
        item.Title = title;
        item.Detail = detail;
        item.DelayMs = kind == NotificationKind.Progress ? ProgressDelayMs : 0;
        item.RemainingMs = SuccessMs;
    }

    public void Dismiss(long id) => _items.RemoveAll(item => item.Id == id);

    public void Advance(double elapsedMs)
    {
        foreach (var item in _items)
        {
            item.DelayMs = Math.Max(0, item.DelayMs - elapsedMs);
        }

        if (Paused)
        {
            return;
        }

        foreach (var item in Visible.Where(item => item.Kind == NotificationKind.Success))
        {
            item.RemainingMs -= elapsedMs;
        }

        _items.RemoveAll(item => item.Kind == NotificationKind.Success && item.RemainingMs <= 0);
    }

    private IEnumerable<Notification> Eligible() =>
        Enumerable.Reverse(_items).Where(item => item.DelayMs <= 0);
}
