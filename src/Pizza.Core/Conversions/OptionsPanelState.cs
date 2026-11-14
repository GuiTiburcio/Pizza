using System.Globalization;

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
    ShiftLeft,
    ShiftRight,
    Home,
    End,
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
    Slider,
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
    private const int ThumbRadius = 14;
    private const int Full = 100;
    private const int BigStep = 10;
    private const int MaxDigits = 3;
    private const int BoxWidth = 64;
    private const int StepsBoxWidth = 96;
    private const int BoxHeight = 28;
    private const int BoxRaise = 6;

    private readonly int[] _selected;
    private readonly Dictionary<string, string> _advanced;
    private readonly int[] _percent;
    private string? _typed;
    private bool _editing;
    private readonly List<(PanelTargetKind Kind, int Row)> _stops = [];
    private readonly List<PanelTarget> _targets = [];
    private int _focus;

    public OptionsPanelState(ConverterInfo converter)
    {
        Converter = converter;
        Rows = converter.Options.Where(option => option.Key != ConverterCatalog.MetadataKey && !option.Advanced).ToArray();
        _advanced = converter.Options.Where(option => option.Advanced).ToDictionary(option => option.Key, option => option.Default);
        CanKeepMetadata = converter.Options.Any(option => option.Key == ConverterCatalog.MetadataKey);
        _selected = Rows.Select(option => option.Choices.ToList().FindIndex(choice => choice.Value == option.Default)).ToArray();

        _percent = Rows.Select(option => option.Minimum is null || option.Default == ConverterOption.Original ? Full : int.Parse(option.Default, CultureInfo.InvariantCulture)).ToArray();

        var top = FirstRowTop;
        for (var row = 0; row < Rows.Count; row++)
        {
            if (IsSlider(row))
            {
                // ADR-030: a régua ocupa a altura da linha de botões que substitui.
                _stops.Add((PanelTargetKind.Slider, row));
                _targets.Add(new PanelTarget(PanelTargetKind.Slider, row, 0, new PanelRect(Padding, top + PillTop, PanelWidth - Padding, top + PillTop + PillHeight)));
                top += PillTop + PillHeight + RowGap;
                continue;
            }

            _stops.Add((PanelTargetKind.Choice, row));
            AddPills(row, top + PillTop);
            top += PillTop + PillHeight + RowGap;
        }

        KeepTop = top;
        if (CanKeepMetadata && HasMetadataRow)
        {
            _stops.Add((PanelTargetKind.Keep, 0));
            _targets.Add(new PanelTarget(PanelTargetKind.Keep, 0, 0, new PanelRect(Padding, top, PanelWidth - Padding, top + KeepRowHeight)));
        }

        // O espaço do aviso "Este formato não guarda metadados" só existe quando ele aparece (prancha 4d-1).
        top += (HasMetadataRow ? KeepRowHeight + (CanKeepMetadata ? 0 : NoticeHeight) : 0) + ButtonsGap;
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

    /// <summary>ADR-035: no áudio a saída sai sem etiquetas e a linha "Manter metadados" não aparece.</summary>
    public bool HasMetadataRow => Converter.Tab != ConverterCatalog.AudioTab;

    public bool KeepMetadata { get; private set; }

    public int Width => PanelWidth;

    public int Height { get; }

    /// <summary>Topo da linha "Manter metadados"; o aviso de formato sem metadados vem logo abaixo.</summary>
    public int KeepTop { get; }

    public IReadOnlyList<PanelTarget> Targets => _targets;

    public (PanelTargetKind Kind, int Row) Focus => _stops[_focus];

    public static int RowLabelTop(int row) => FirstRowTop + (row * (PillTop + PillHeight + RowGap));

    public bool IsSelected(int row, int choice) => _selected[row] == choice;

    public bool IsSlider(int row) => Rows[row].Minimum is not null || Rows[row].Steps;

    /// <summary>Valor da régua, de Minimum a 100 (100 é o original).</summary>
    public int Percent(int row) => IsTyping(row) ? Clamp(row, TypedValue()) : _percent[row];

    /// <summary>O que a caixa ao lado do título mostra: o que está sendo digitado, ou o valor.</summary>
    public string ValueText(int row) =>
        IsTyping(row) ? _typed!
        : Rows[row].Steps ? (IsNumber(Rows[row].Choices[_selected[row]].Value) ? Rows[row].Choices[_selected[row]].Value : Rows[row].Choices[_selected[row]].Label)
        : _percent[row].ToString(CultureInfo.InvariantCulture);

    /// <summary>Unidade depois do número na caixa: "%" na régua de porcentagem; na de degraus, a do rótulo ("kbps").</summary>
    public string ValueUnit(int row)
    {
        if (!Rows[row].Steps)
        {
            return "%";
        }

        var choice = Rows[row].Choices[_selected[row]];
        return IsNumber(choice.Value) && choice.Label.StartsWith(choice.Value + " ", StringComparison.Ordinal) ? choice.Label[(choice.Value.Length + 1)..] : "";
    }

    /// <summary>Caixa do valor, à direita do título da régua, acima do trilho.</summary>
    public PanelRect ValueBox(int row)
    {
        var top = RowLabelTop(row) - BoxRaise;
        return new PanelRect(PanelWidth - Padding - (Rows[row].Steps ? StepsBoxWidth : BoxWidth), top, PanelWidth - Padding, top + BoxHeight);
    }

    /// <summary>Linha da régua cuja caixa do valor está sob o ponto.</summary>
    public int? ValueBoxAt(int x, int y)
    {
        for (var row = 0; row < Rows.Count; row++)
        {
            if (IsSlider(row) && ValueBox(row).Contains(x, y))
            {
                return row;
            }
        }

        return null;
    }

    /// <summary>Clique na caixa: foca a régua com o valor selecionado; o primeiro dígito o substitui.</summary>
    public void EditValue(int row)
    {
        Commit();
        _focus = _stops.IndexOf((PanelTargetKind.Slider, row));
        _editing = true;
    }

    public bool IsEditing(int row) => _editing && Focus == (PanelTargetKind.Slider, row);

    /// <summary>Trilho da régua: o centro do pino vai de uma ponta à outra, a 14 px da borda do alvo.</summary>
    public PanelRect SliderTrack(int row)
    {
        var bounds = _targets.First(target => target.Kind == PanelTargetKind.Slider && target.Row == row).Bounds;
        return bounds with { Left = bounds.Left + ThumbRadius, Right = bounds.Right - ThumbRadius };
    }

    /// <summary>Posição do pino, de 0 (mínimo) a 1 (original).</summary>
    public double SliderAmount(int row) =>
        Rows[row].Steps ? StepAt(row) / (double)(Rows[row].Choices.Count - 1)
        : (Percent(row) - Rows[row].Minimum!.Value) / (double)(Full - Rows[row].Minimum!.Value);

    /// <summary>Arrastar ou clicar na régua: o valor exato sob o cursor, sem degraus.</summary>
    public void Slide(int row, int x)
    {
        Commit();
        _focus = _stops.IndexOf((PanelTargetKind.Slider, row));
        var track = SliderTrack(row);
        var amount = Math.Clamp((x - track.Left) / (double)(track.Right - track.Left), 0, 1);
        if (Rows[row].Steps)
        {
            // ADR-035: o pino para no degrau mais próximo; o codificador só aceita esses valores.
            _selected[row] = (int)Math.Round(amount * (Rows[row].Choices.Count - 1), MidpointRounding.AwayFromZero);
            return;
        }

        var minimum = Rows[row].Minimum!.Value;
        _percent[row] = (int)Math.Round(minimum + (amount * (Full - minimum)), MidpointRounding.AwayFromZero);
    }

    /// <summary>Roda sobre a régua: um ponto por dente, sem mudar o foco.</summary>
    public void Nudge(int row, int steps)
    {
        Commit();
        if (Rows[row].Steps)
        {
            _selected[row] = StepClamp(row, _selected[row] + steps);
            return;
        }

        _percent[row] = Clamp(row, _percent[row] + steps);
    }

    /// <summary>Dígito na caixa da régua em foco; outro caractere é ignorado.</summary>
    public void Type(char key)
    {
        if (Focus.Kind != PanelTargetKind.Slider || !char.IsAsciiDigit(key))
        {
            return;
        }

        _typed = _typed is null ? key.ToString() : _typed.Length < MaxDigits ? _typed + key : _typed;
    }

    public void Erase()
    {
        if (Focus.Kind == PanelTargetKind.Slider)
        {
            // Com o valor selecionado (clique na caixa), apagar limpa tudo.
            var text = _typed ?? (IsEditing(Focus.Row) ? "x" : ValueText(Focus.Row));
            _typed = text.Length > 0 ? text[..^1] : text;
        }
    }

    public int Selected(int row) => _selected[row];

    /// <summary>
    /// Bolha de vidro da opção escolhida (ADR-027) numa posição contínua: 0,5 fica entre a primeira e a segunda
    /// opção do grupo. Fora do grupo, fica na opção da ponta.
    /// </summary>
    public (double Left, double Right, int Top, int Bottom) BubbleAt(int row, double position)
    {
        var last = Rows[row].Choices.Count - 1;
        position = Math.Clamp(position, 0, last);
        var from = (int)Math.Floor(position);
        var to = Math.Min(from + 1, last);
        var a = ChoiceBounds(row, from);
        var b = ChoiceBounds(row, to);
        var amount = position - from;
        return (a.Left + ((b.Left - a.Left) * amount), a.Right + ((b.Right - a.Right) * amount), a.Top, a.Bottom);
    }

    private PanelRect ChoiceBounds(int row, int choice) =>
        _targets.First(target => target.Kind == PanelTargetKind.Choice && target.Row == row && target.Choice == choice).Bounds;

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
        Commit();
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

            case PanelKey.Left or PanelKey.Right or PanelKey.ShiftLeft or PanelKey.ShiftRight when Focus.Kind == PanelTargetKind.Slider && Rows[Focus.Row].Steps:
                _selected[Focus.Row] = StepClamp(Focus.Row, _selected[Focus.Row] + (key is PanelKey.Left or PanelKey.ShiftLeft ? -1 : 1));
                return PanelAction.None;

            case PanelKey.Home or PanelKey.End when Focus.Kind == PanelTargetKind.Slider && Rows[Focus.Row].Steps:
                _selected[Focus.Row] = key == PanelKey.Home ? 0 : Rows[Focus.Row].Choices.Count - 1;
                return PanelAction.None;

            case PanelKey.Left or PanelKey.Right or PanelKey.ShiftLeft or PanelKey.ShiftRight when Focus.Kind == PanelTargetKind.Slider:
                var step = key switch { PanelKey.Left => -1, PanelKey.Right => 1, PanelKey.ShiftLeft => -BigStep, _ => BigStep };
                _percent[Focus.Row] = Clamp(Focus.Row, _percent[Focus.Row] + step);
                return PanelAction.None;

            case PanelKey.Home or PanelKey.End when Focus.Kind == PanelTargetKind.Slider:
                _percent[Focus.Row] = key == PanelKey.Home ? Rows[Focus.Row].Minimum!.Value : Full;
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
        Commit();
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
            options[Rows[row].Key] = !IsSlider(row) || Rows[row].Steps ? Rows[row].Choices[StepAt(row)].Value
                : Percent(row) == Full ? ConverterOption.Original
                : Percent(row).ToString(CultureInfo.InvariantCulture);
        }

        foreach (var (key, value) in _advanced)
        {
            options[key] = value;
        }

        if (CanKeepMetadata)
        {
            options[ConverterCatalog.MetadataKey] = KeepMetadata ? ConverterCatalog.MetadataKeep : ConverterCatalog.MetadataStrip;
        }

        return options;
    }

    /// <summary>Há dígitos sendo digitados na caixa desta régua.</summary>
    public bool IsTyping(int row) => _typed is not null && Focus == (PanelTargetKind.Slider, row);

    /// <summary>O que foi digitado vale ao sair da caixa, limitado à régua.</summary>
    private void Commit()
    {
        _editing = false;
        if (_typed is not null)
        {
            if (Rows[Focus.Row].Steps)
            {
                _selected[Focus.Row] = StepAt(Focus.Row);
            }
            else
            {
                _percent[Focus.Row] = Clamp(Focus.Row, TypedValue());
            }

            _typed = null;
        }
    }

    /// <summary>Degrau escolhido; com dígitos na caixa, o degrau numérico mais próximo do que foi digitado.</summary>
    private int StepAt(int row)
    {
        if (!IsTyping(row) || _typed!.Length == 0)
        {
            return _selected[row];
        }

        var typed = TypedValue();
        var choices = Rows[row].Choices;
        return Enumerable.Range(0, choices.Count)
            .Where(index => IsNumber(choices[index].Value))
            .MinBy(index => Math.Abs(int.Parse(choices[index].Value, CultureInfo.InvariantCulture) - typed));
    }

    private int StepClamp(int row, int index) => Math.Clamp(index, 0, Rows[row].Choices.Count - 1);

    private static bool IsNumber(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);

    private int TypedValue() => _typed is { Length: > 0 } text ? int.Parse(text, CultureInfo.InvariantCulture) : 0;

    private int Clamp(int row, int value) => Math.Clamp(value, Rows[row].Minimum!.Value, Full);

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
