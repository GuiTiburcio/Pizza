namespace Pizza.Core.Conversions;

public enum PanelKey
{
    Tab,
    ShiftTab,
    Left,
    Right,
    Up,
    Down,
    Space,
    Enter,
    Escape,
}

public enum PanelAction
{
    None,
    Convert,
    Cancel,
}

public enum PanelTargetKind
{
    Choice,
    Keep,
    Cancel,
    Convert,
}

public readonly record struct PanelRect(int Left, int Top, int Right, int Bottom)
{
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;

    public bool Overlaps(PanelRect other) => Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;
}

/// <summary>Algo clicável no painel. Medidas em pixels a 96 dpi.</summary>
public readonly record struct PanelTarget(PanelTargetKind Kind, int Row, int Choice, PanelRect Bounds);

/// <summary>Estado e geometria do painel de opções (WF-3), montados a partir do catálogo.</summary>
public sealed class OptionsPanelState
{
    public const int Padding = 24;
    public const int LabelHeight = 20;
    public const int KeepRowHeight = 44;

    private const int PanelWidth = 480;
    private const int FirstRowTop = 88;
    private const int PillTop = 22;
    private const int PillHeight = 44;
    private const int RowGap = 14;
    private const int Gap = 8;
    private const int NoticeHeight = 20;
    private const int ButtonWidth = 140;
    private const int ButtonHeight = 44;
    private const int ButtonsGap = 20;

    private readonly int[] _selected;
    private readonly List<(PanelTargetKind Kind, int Row)> _stops = [];
    private readonly List<PanelTarget> _targets = [];
    private int _focus;

    public OptionsPanelState(ConverterInfo converter)
    {
        Converter = converter;
        Rows = converter.Options.Where(option => option.Key != ConverterCatalog.MetadataKey).ToArray();
        CanKeepMetadata = converter.Options.Any(option => option.Key == ConverterCatalog.MetadataKey);
        _selected = Rows.Select(option => option.Choices.ToList().FindIndex(choice => choice.Value == option.Default)).ToArray();

        var top = FirstRowTop;
        for (var row = 0; row < Rows.Count; row++)
        {
            _stops.Add((PanelTargetKind.Choice, row));
            AddPills(row, top + PillTop);
            top += PillTop + PillHeight + RowGap;
        }

        KeepTop = top;
        if (CanKeepMetadata)
        {
            _stops.Add((PanelTargetKind.Keep, 0));
            _targets.Add(new PanelTarget(PanelTargetKind.Keep, 0, 0, new PanelRect(Padding, top, PanelWidth - Padding, top + KeepRowHeight)));
        }

        top += KeepRowHeight + NoticeHeight + ButtonsGap;
        var cancelLeft = PanelWidth - Padding - (2 * ButtonWidth) - Gap;
        _stops.Add((PanelTargetKind.Cancel, 0));
        _targets.Add(new PanelTarget(PanelTargetKind.Cancel, 0, 0, new PanelRect(cancelLeft, top, cancelLeft + ButtonWidth, top + ButtonHeight)));
        _stops.Add((PanelTargetKind.Convert, 0));
        _targets.Add(new PanelTarget(PanelTargetKind.Convert, 0, 0, new PanelRect(cancelLeft + ButtonWidth + Gap, top, PanelWidth - Padding, top + ButtonHeight)));

        Height = top + ButtonHeight + Padding;
    }

    public ConverterInfo Converter { get; }

    public IReadOnlyList<ConverterOption> Rows { get; }

    public bool CanKeepMetadata { get; }

    public bool KeepMetadata { get; private set; }

    public int Width => PanelWidth;

    public int Height { get; }

    /// <summary>Topo da linha "Manter metadados"; o aviso de formato sem metadados vem logo abaixo.</summary>
    public int KeepTop { get; }

    public IReadOnlyList<PanelTarget> Targets => _targets;

    public (PanelTargetKind Kind, int Row) Focus => _stops[_focus];

    public static int RowLabelTop(int row) => FirstRowTop + (row * (PillTop + PillHeight + RowGap));

    public bool IsSelected(int row, int choice) => _selected[row] == choice;

    public PanelTarget? TargetAt(int x, int y)
    {
        foreach (var target in _targets)
        {
            if (target.Bounds.Contains(x, y))
            {
                return target;
            }
        }

        return null;
    }

    public PanelAction Press(PanelKey key)
    {
        switch (key)
        {
            case PanelKey.Escape:
                return PanelAction.Cancel;

            case PanelKey.Enter:
                return Focus.Kind == PanelTargetKind.Cancel ? PanelAction.Cancel : PanelAction.Convert;

            case PanelKey.Tab or PanelKey.Down:
                _focus = (_focus + 1) % _stops.Count;
                return PanelAction.None;

            case PanelKey.ShiftTab or PanelKey.Up:
                _focus = (_focus + _stops.Count - 1) % _stops.Count;
                return PanelAction.None;

            case PanelKey.Left or PanelKey.Right when Focus.Kind == PanelTargetKind.Choice:
                var last = Rows[Focus.Row].Choices.Count - 1;
                _selected[Focus.Row] = Math.Clamp(_selected[Focus.Row] + (key == PanelKey.Right ? 1 : -1), 0, last);
                return PanelAction.None;

            case PanelKey.Space when Focus.Kind == PanelTargetKind.Keep:
                KeepMetadata = !KeepMetadata;
                return PanelAction.None;

            case PanelKey.Space when Focus.Kind == PanelTargetKind.Cancel:
                return PanelAction.Cancel;

            case PanelKey.Space when Focus.Kind == PanelTargetKind.Convert:
                return PanelAction.Convert;

            default:
                return PanelAction.None;
        }
    }

    public PanelAction Click(PanelTarget target)
    {
        _focus = _stops.IndexOf((target.Kind, target.Row));
        switch (target.Kind)
        {
            case PanelTargetKind.Choice:
                _selected[target.Row] = target.Choice;
                return PanelAction.None;

            case PanelTargetKind.Keep:
                KeepMetadata = !KeepMetadata;
                return PanelAction.None;

            case PanelTargetKind.Cancel:
                return PanelAction.Cancel;

            default:
                return PanelAction.Convert;
        }
    }

    /// <summary>As escolhas no formato que o Worker valida.</summary>
    public IReadOnlyDictionary<string, string> Options()
    {
        var options = new Dictionary<string, string>();
        for (var row = 0; row < Rows.Count; row++)
        {
            options[Rows[row].Key] = Rows[row].Choices[_selected[row]].Value;
        }

        if (CanKeepMetadata)
        {
            options[ConverterCatalog.MetadataKey] = KeepMetadata ? ConverterCatalog.MetadataKeep : ConverterCatalog.MetadataStrip;
        }

        return options;
    }

    private void AddPills(int row, int top)
    {
        var count = Rows[row].Choices.Count;
        var inner = PanelWidth - (2 * Padding);
        var width = (inner - ((count - 1) * Gap)) / count;
        for (var choice = 0; choice < count; choice++)
        {
            var left = Padding + (choice * (width + Gap));
            _targets.Add(new PanelTarget(PanelTargetKind.Choice, row, choice, new PanelRect(left, top, left + width, top + PillHeight)));
        }
    }
}
