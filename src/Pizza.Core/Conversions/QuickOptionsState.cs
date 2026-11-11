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
        Primary = converter.Options.FirstOrDefault(option => option.Key != ConverterCatalog.MetadataKey);
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
        Primary = null;
        Selected = 0;
    }
}
