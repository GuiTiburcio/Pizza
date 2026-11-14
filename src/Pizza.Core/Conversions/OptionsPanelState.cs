using System.Globalization;
using Pizza.Core.Ipc;

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

    /// <summary>"Ajustar…" na linha Volume: a janela troca para a página Volume (prancha AU-5).</summary>
    OpenVolume,
}

/// <summary>A página principal do painel, ou a página Volume do áudio (adendo do ADR-035).</summary>
public enum PanelPage
{
    Main,
    Volume,
}

public enum PanelTargetKind
{
    Choice,
    Slider,

    /// <summary>Caixinha que liga e desliga um efeito na página Volume.</summary>
    Toggle,
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
    private const int SizeRowHeight = 28;
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

    public const string VolumeKey = "volume";

    // Linha Volume do painel principal (prancha AU-3): não vai para o Worker; o que vale são as opções avançadas.
    private static readonly ConverterOption VolumeRow = new(VolumeKey, "Volume", "original", [new("original", "Original"), new("adjust", "Ajustar…")]);

    // Valor de cada efeito quando ele é ligado pela primeira vez (adendo do ADR-035).
    private static readonly Dictionary<string, string> EffectDefaults = new() { ["gain"] = "3", ["compress"] = "-18", ["normalize"] = "-14", ["limit"] = "-1" };

    private static readonly Dictionary<string, string> EffectNames = new() { ["gain"] = "Amplificar", ["compress"] = "Comprimir", ["normalize"] = "Normalizar", ["limit"] = "Limitar" };

    private readonly bool[] _enabled;

    public OptionsPanelState(ConverterInfo converter)
        : this(
            converter,
            PanelPage.Main,
            [
                .. converter.Options.Where(option => option.Key != ConverterCatalog.MetadataKey && !option.Advanced),
                .. converter.Options.Any(option => option.Advanced) ? [VolumeRow] : Array.Empty<ConverterOption>(),
            ],
            converter.Options.Where(option => option.Advanced).ToDictionary(option => option.Key, option => option.Default),
            null)
    {
    }

    private OptionsPanelState(ConverterInfo converter, PanelPage page, IReadOnlyList<ConverterOption> rows, Dictionary<string, string> advanced, bool[]? enabled)
    {
        Converter = converter;
        Page = page;
        Rows = rows;
        _advanced = advanced;
        _enabled = enabled ?? new bool[rows.Count];
        CanKeepMetadata = page == PanelPage.Main && converter.Options.Any(option => option.Key == ConverterCatalog.MetadataKey);
        _selected = Rows.Select(option => option.Choices.ToList().FindIndex(choice => choice.Value == option.Default)).ToArray();
        SyncVolumeRow();

        _percent = Rows.Select(option => option.Minimum is null || option.Default == ConverterOption.Original ? Full : int.Parse(option.Default, CultureInfo.InvariantCulture)).ToArray();

        var top = FirstRowTop;
        for (var row = 0; row < Rows.Count; row++)
        {
            if (IsToggle(row))
            {
                // Prancha AU-5: a caixinha fica na linha do título, à esquerda da caixa do valor.
                var labelTop = RowLabelTop(row);
                _stops.Add((PanelTargetKind.Toggle, row));
                _targets.Add(new PanelTarget(PanelTargetKind.Toggle, row, 0, new PanelRect(Padding, labelTop - 6, PanelWidth - Padding - StepsBoxWidth - 8, labelTop + LabelHeight + 6)));
            }

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
        SizeTop = top - ButtonsGap;
        if (HasSizeRow)
        {
            // Adendo do ADR-035: o tamanho previsto fica numa linha logo acima dos botões.
            top += SizeRowHeight;
        }
        var cancelLeft = PanelWidth - Padding - (2 * ButtonWidth) - Gap;
        _stops.Add((PanelTargetKind.Cancel, 0));
        _targets.Add(new PanelTarget(PanelTargetKind.Cancel, 0, 0, new PanelRect(cancelLeft, top, cancelLeft + ButtonWidth, top + ButtonHeight)));
        _stops.Add((PanelTargetKind.Convert, 0));
        _targets.Add(new PanelTarget(PanelTargetKind.Convert, 0, 0, new PanelRect(cancelLeft + ButtonWidth + Gap, top, PanelWidth - Padding, top + ButtonHeight)));

        Height = top + ButtonHeight + Padding;
    }

    public ConverterInfo Converter { get; }

    public PanelPage Page { get; }

    /// <summary>Linha "Tamanho previsto" no painel principal do áudio.</summary>
    public bool HasSizeRow => Page == PanelPage.Main && Converter.Tab == ConverterCatalog.AudioTab;

    /// <summary>Topo da linha do tamanho previsto.</summary>
    public int SizeTop { get; }

    private MediaInfo? _info;

    /// <summary>O "info" do Worker chegou; o tamanho passa a acompanhar as opções.</summary>
    public void SetInfo(MediaInfo info) => _info = info;

    /// <summary>Tamanho previsto com as opções de agora; "—" enquanto o Worker não respondeu (ou se não respondeu).</summary>
    public string SizeText => _info is { } info && HasSizeRow ? AudioPlan.Estimate(Converter.Id, Options(), info).Text : "—";

    /// <summary>Caixinha de efeito: na página Volume, as linhas que têm "Desligado" no catálogo.</summary>
    public bool IsToggle(int row) => Page == PanelPage.Volume && EffectDefaults.ContainsKey(Rows[row].Key);

    /// <summary>Efeito ligado; linhas sem caixinha contam como ligadas.</summary>
    public bool IsEnabled(int row) => !IsToggle(row) || _enabled[row];

    /// <summary>Resumo ao lado do título da linha Volume, como "Normalizar · Limitar"; null em Original.</summary>
    public string? RowNote(int row)
    {
        if (Page != PanelPage.Main || Rows[row].Key != VolumeKey || !AnyEffect)
        {
            return null;
        }

        return string.Join(" · ", EffectNames.Where(effect => _advanced[effect.Key] != ConverterCatalog.Off).Select(effect => effect.Value));
    }

    /// <summary>
    /// A página Volume, a partir do que já foi aplicado. Na primeira vez, Normalizar (-14 LUFS) e Limitar (-1 dB) vêm
    /// ligados (adendo do ADR-035).
    /// </summary>
    public OptionsPanelState VolumePage()
    {
        var first = !AnyEffect;
        var rows = new List<ConverterOption>();
        var enabled = new List<bool>();
        foreach (var option in Converter.Options.Where(option => option.Advanced))
        {
            var value = _advanced[option.Key];
            if (EffectDefaults.TryGetValue(option.Key, out var fallback))
            {
                rows.Add(option with { Choices = option.Choices.Where(choice => choice.Value != ConverterCatalog.Off).ToArray(), Default = value == ConverterCatalog.Off ? fallback : value });
                enabled.Add(value != ConverterCatalog.Off || (first && option.Key is "normalize" or "limit"));
            }
            else
            {
                rows.Add(option with { Default = value });
                enabled.Add(true);
            }
        }

        return new OptionsPanelState(Converter, PanelPage.Volume, rows, new Dictionary<string, string>(), enabled.ToArray());
    }

    /// <summary>Aplicar na página Volume: os efeitos passam a valer e a linha Volume mostra "Ajustar…".</summary>
    public void ApplyVolume(OptionsPanelState volume)
    {
        foreach (var (key, value) in volume.Options())
        {
            _advanced[key] = value;
        }

        SyncVolumeRow();
    }

    /// <summary>Voltar sem aplicar: a linha Volume reflete de novo o que já valia.</summary>
    public void CancelVolume() => SyncVolumeRow();

    private bool AnyEffect => _advanced.Any(option => EffectDefaults.ContainsKey(option.Key) && option.Value != ConverterCatalog.Off);

    private void SyncVolumeRow()
    {
        if (Page == PanelPage.Main && Rows.ToList().FindIndex(row => row.Key == VolumeKey) is var volume and >= 0)
        {
            _selected[volume] = AnyEffect ? 1 : 0;
        }
    }

    public IReadOnlyList<ConverterOption> Rows { get; }

    public bool CanKeepMetadata { get; }

    /// <summary>ADR-035: no áudio a saída sai sem etiquetas e a linha "Manter metadados" não aparece.</summary>
    public bool HasMetadataRow => Page == PanelPage.Main && Converter.Tab != ConverterCatalog.AudioTab;

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
        : Rows[row].Steps ? (ValueUnit(row).Length > 0 ? Rows[row].Choices[_selected[row]].Value : Rows[row].Choices[_selected[row]].Label)
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
        TurnOn(row);
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
            TurnOn(row);
            _selected[row] = StepClamp(row, _selected[row] + steps);
            return;
        }

        _percent[row] = Clamp(row, _percent[row] + steps);
    }

    /// <summary>Dígito na caixa da régua em foco; outro caractere é ignorado.</summary>
    public void Type(char key)
    {
        if (Focus.Kind != PanelTargetKind.Slider || !char.IsAsciiDigit(key)
            || (Rows[Focus.Row].Steps && !Rows[Focus.Row].Choices.Any(choice => IsNumber(choice.Value))))
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
                TurnOn(Focus.Row);
                _selected[Focus.Row] = StepClamp(Focus.Row, _selected[Focus.Row] + (key is PanelKey.Left or PanelKey.ShiftLeft ? -1 : 1));
                return PanelAction.None;

            case PanelKey.Home or PanelKey.End when Focus.Kind == PanelTargetKind.Slider && Rows[Focus.Row].Steps:
                TurnOn(Focus.Row);
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
                return Choose(Focus.Row, Math.Clamp(_selected[Focus.Row] + (key == PanelKey.Right ? 1 : -1), 0, last));

            case PanelKey.Space when Focus.Kind == PanelTargetKind.Toggle:
                _enabled[Focus.Row] = !_enabled[Focus.Row];
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
                return Choose(target.Row, target.Choice);

            case PanelTargetKind.Toggle:
                _enabled[target.Row] = !_enabled[target.Row];
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
            if (Rows[row].Key == VolumeKey)
            {
                continue;
            }

            if (!IsEnabled(row))
            {
                options[Rows[row].Key] = ConverterCatalog.Off;
                continue;
            }

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

    private void TurnOn(int row)
    {
        if (IsToggle(row))
        {
            _enabled[row] = true;
        }
    }

    /// <summary>Escolha num grupo de botões. Na linha Volume, "Ajustar…" abre a página e "Original" desliga os efeitos.</summary>
    private PanelAction Choose(int row, int choice)
    {
        _selected[row] = choice;
        if (Rows[row].Key != VolumeKey)
        {
            return PanelAction.None;
        }

        if (choice == 1)
        {
            return PanelAction.OpenVolume;
        }

        foreach (var key in EffectDefaults.Keys)
        {
            _advanced[key] = ConverterCatalog.Off;
        }

        return PanelAction.None;
    }

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
