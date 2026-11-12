namespace Pizza.Core.Conversions;

/// <summary>Contexto da faixa da 4b (ADR-027). Não confirma nem executa conversões.</summary>
public sealed class QuickOptionsState
{
    public ConverterInfo? Converter { get; private set; }
    public ConverterOption? Primary { get; private set; }
    public int Selected { get; private set; }

    /// <summary>Espaço vazio preserva o contexto; outra fatia restaura os padrões.</summary>
    public void PointAt(ConverterInfo? converter)
    {
        if (converter is null || converter.Id == Converter?.Id) return;
        Converter = converter;
        _wheelDelta = 0;
        Primary = converter.Options.FirstOrDefault(option => option.Key != ConverterCatalog.MetadataKey);
        if (Primary?.QuickChoices is { } quick) Primary = Primary with { Choices = quick };
        Selected = Primary is null ? 0 : Primary.Choices.ToList().FindIndex(choice => choice.Value == Primary.Default);
    }

    /// <summary>Roda/teclado só alteram a prévia, sem dar a volta nas extremidades.</summary>
    public void Step(int direction)
    {
        if (Primary is not null)
        {
            Selected = (int)Math.Clamp((long)Selected + direction, 0, Primary.Choices.Count - 1);
        }
    }

    /// <summary>Um dente da roda. Touchpads e rodas de alta precisão enviam frações disso.</summary>
    private const int WheelNotch = 120;

    private int _wheelDelta;

    /// <summary>Título da faixa, como "Qualidade do WEBP" (prancha 4b-1).</summary>
    /// <summary>Na aba PDF a opção já diz a ação, como "Girar páginas" (prancha 6A-1).</summary>
    public string? Title => Primary is null ? null : Converter!.Tab == ConverterCatalog.ImageTab ? $"{Primary.Label} do {Converter.Label}" : Primary.Label;

    /// <summary>Valor embaixo do nome do nível; o padrão ganha "padrão". Null quando o nome já diz o valor.</summary>
    public string? Detail(int level)
    {
        if (Primary is null)
        {
            return null;
        }

        var choice = Primary.Choices[level];
        var value = (Primary.Key, choice.Value) switch
        {
            ("quality", var quality) => $"{quality}%",
            ("bitrate", "lossless") => "Sem perda",
            ("bitrate", var bitrate) => $"{bitrate} kbps",
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
        return ConverterCatalog.ResolveOptions(Converter,
            new Dictionary<string, string> { [Primary.Key] = Primary.Choices[Selected].Value });
    }

    public void Clear()
    {
        Converter = null;
        _wheelDelta = 0;
        Primary = null;
        Selected = 0;
    }
}
