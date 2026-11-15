namespace Pizza.Core.Conversions;

/// <summary>Contexto da faixa da 4b (ADR-027). Não confirma nem executa conversões.</summary>
public sealed class QuickOptionsState
{
    public ConverterInfo? Converter { get; private set; }
    public ConverterOption? Primary { get; private set; }

    /// <summary>Níveis da faixa: os atalhos da opção, quando ela tem (prancha AU-2), ou todas as escolhas.</summary>
    public IReadOnlyList<OptionChoice> Levels => Primary is null ? []
        : _custom is { } custom ? [.. (Primary.Quick ?? Primary.Choices), custom] : Primary.Quick ?? Primary.Choices;
    public int Selected { get; private set; }
    private IReadOnlyDictionary<string, string>? _options;
    private OptionChoice? _custom;

    /// <summary>Espaço vazio preserva o contexto; outra fatia restaura os padrões.</summary>
    public void PointAt(ConverterInfo? converter)
    {
        if (converter is null || converter.Id == Converter?.Id) return;
        Converter = converter;
        _options = null;
        _custom = null;
        _wheelDelta = 0;
        Primary = converter.Options.FirstOrDefault(option => option.Key != ConverterCatalog.MetadataKey && !option.Advanced);
        Selected = Primary is null ? 0 : Levels.ToList().FindIndex(choice => choice.Value == Primary.Default);
    }

    /// <summary>Retoma as opções do painel, inclusive um valor que não está entre os atalhos da faixa.</summary>
    public void Restore(ConverterInfo converter, IReadOnlyDictionary<string, string> options)
    {
        var resolved = ConverterCatalog.ResolveOptions(converter, options)
            ?? throw new ArgumentException("As opções não pertencem ao conversor.", nameof(options));
        PointAt(converter);
        _options = resolved;
        _custom = null;
        if (Primary is { } primary)
        {
            var value = resolved[primary.Key];
            if (!Levels.Any(choice => choice.Value == value))
                _custom = primary.Choices.FirstOrDefault(choice => choice.Value == value)
                    ?? new OptionChoice(value, primary.Minimum is not null ? $"{value}%" : value);
            Selected = Levels.ToList().FindIndex(choice => choice.Value == value);
        }
    }

    /// <summary>Roda/teclado só alteram a prévia, sem dar a volta nas extremidades.</summary>
    public void Step(int direction)
    {
        if (Primary is not null)
        {
            Selected = (int)Math.Clamp((long)Selected + direction, 0, Levels.Count - 1);
        }
    }

    /// <summary>Um dente da roda. Touchpads e rodas de alta precisão enviam frações disso.</summary>
    private const int WheelNotch = 120;

    private int _wheelDelta;

    /// <summary>Título da faixa, como "Qualidade do WEBP" (prancha 4b-1).</summary>
    /// <summary>Na aba PDF a opção já diz a ação, como "Girar páginas" (prancha 6A-1).</summary>
    public string? Title => Primary is null ? null
        : Converter!.Tab is ConverterCatalog.ImageTab or ConverterCatalog.AudioTab ? $"{Primary.Label} do {Converter.Label}" : Primary.Label;

    /// <summary>Valor embaixo do nome do nível; o padrão ganha "padrão". Null quando o nome já diz o valor.</summary>
    public string? Detail(int level)
    {
        if (Primary is null)
        {
            return null;
        }

        var choice = Levels[level];
        var value = (Primary.Key, choice.Value) switch
        {
            ("quality", var quality) => $"{quality}%",
            ("bitrate", var kbps) => $"{kbps} kbps",
            ("depth", "16") => "CD",
            ("depth", "24") => "estúdio",
            ("mode", "pages") => "1 arquivo por pág.",
            ("mode", "half") => "2 arquivos",
            ("size", var size) => $"{size} px",
            ("rotation", "270") => "90°",
            ("rotation", var degrees) => $"{degrees}°",
            ("resolution", var dpi) when Converter!.Extension == "pdf" && dpi != "original" => $"{dpi} dpi",
            _ => null,
        };

        if (value == choice.Label)
        {
            value = null;
        }

        return choice.Value != Primary.Default ? value : value is null ? "padrão" : $"{value} · padrão";
    }

    /// <summary>Roda sobre a faixa (ADR-027): para baixo avança, como na troca de aba. Devolve verdadeiro se o nível mudou.</summary>
    public bool Wheel(int delta)
    {
        if (Primary is null || delta == 0)
        {
            return false;
        }

        if (Math.Sign(delta) != Math.Sign(_wheelDelta))
        {
            _wheelDelta = 0;
        }

        _wheelDelta += delta;
        var notches = _wheelDelta / WheelNotch;
        _wheelDelta -= notches * WheelNotch;
        var before = Selected;
        Step(-notches);
        return Selected != before;
    }

    /// <summary>Opções para uma confirmação futura: só a principal muda; demais usam padrões do Worker.</summary>
    public IReadOnlyDictionary<string, string>? Options()
    {
        if (Converter is null || Primary is null) return null;
        var options = _options is null ? new Dictionary<string, string>() : new Dictionary<string, string>(_options);
        options[Primary.Key] = Levels[Selected].Value;
        return ConverterCatalog.ResolveOptions(Converter, options);
    }

    public void Clear()
    {
        Converter = null;
        _wheelDelta = 0;
        Primary = null;
        Selected = 0;
        _options = null;
        _custom = null;
    }
}
