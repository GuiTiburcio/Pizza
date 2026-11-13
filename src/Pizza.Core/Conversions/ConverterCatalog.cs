namespace Pizza.Core.Conversions;

/// <summary>Um valor possível de uma opção.</summary>
public sealed record OptionChoice(string Value, string Label);

/// <summary>Uma opção do painel. O padrão é sempre uma das escolhas.</summary>
/// <summary><c>Minimum</c>: a opção é uma régua de porcentagem (ADR-030), de Minimum a 100; 100 é "original".</summary>
public sealed record ConverterOption(string Key, string Label, string Default, IReadOnlyList<OptionChoice> Choices, int? Minimum = null)
{
    public const string Original = "original";

    /// <summary>Valor da régua: só dígitos, sem zero à esquerda, de Minimum a 99; ou "original".</summary>
    public bool Accepts(string value) =>
        Choices.Any(choice => choice.Value == value)
        || (Minimum is { } minimum
            && value.Length is > 0 and <= 2
            && value[0] != '0'
            && value.All(char.IsAsciiDigit)
            && int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) >= minimum);
}

/// <summary>O que o residente precisa saber de uma conversão, sem carregar nenhum decodificador.</summary>
public sealed record ConverterInfo(
    string Id,
    int Tab,
    string Label,
    string Caption,
    string Extension,
    IReadOnlyList<string> InputExtensions,
    IReadOnlyList<ConverterOption> Options,
    string Suffix = "",
    bool Splits = false,
    bool Merges = false,
    bool DocumentEngine = false)
{
    /// <summary>SEC-46: até 500 arquivos por arraste.</summary>
    public const int MaxFiles = 500;

    /// <summary>Sem opções, a conversão começa ao soltar (FR-21).</summary>
    public bool HasPanel => Options.Count > 0;

    /// <summary>
    /// Se a fatia vale para estes arquivos. FR-30 com a emenda da ADR-033: vários arquivos só no Juntar, e o
    /// Juntar só com dois ou mais; todos precisam ser de um tipo que a conversão aceita.
    /// </summary>
    public bool Accepts(IReadOnlyList<string> paths) =>
        (Merges ? paths.Count is >= 2 and <= MaxFiles : paths.Count == 1)
        && paths.All(path => InputExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));

    /// <summary>Legenda da fatia quando ela não vale para o que foi arrastado (prancha 6A-1).</summary>
    public string UnavailableCaption => Merges ? "2 ou mais PDFs" : "um por vez";

    /// <summary>Com várias saídas (Dividir), o sufixo de cada parte; null quando há uma saída só.</summary>
    public Func<int, int, string>? PartSuffix(IReadOnlyDictionary<string, string> options) =>
        Splits ? (part, count) => Output.OutputNaming.PartSuffix(options.GetValueOrDefault("mode", Options[0].Default), part, count) : null;
}

/// <summary>Lista das conversões. O menu e o painel se montam daqui; a ordem é a das fatias (ADR-012).</summary>
public static class ConverterCatalog
{
    public const string MetadataKey = "metadata";
    public const string MetadataStrip = "strip";
    public const string MetadataKeep = "keep";

    public const int ImageTab = 0;
    public const int PdfTab = 1;
    public const int DocsTab = 2;

    // ADR-034: o que o motor de documentos do Pizza Jumbo abre (Writer, Calc e Impress, formatos atuais e antigos).
    private static readonly string[] DocumentInputs = [".docx", ".doc", ".odt", ".rtf", ".xlsx", ".xls", ".ods", ".pptx", ".ppt", ".odp"];

    /// <summary>ADR-034: o motor de documentos existe só no Pizza Jumbo; o residente liga isto ao abrir.</summary>
    public static bool DocumentEngineAvailable { get; set; }

    // SEC-45. HEIC fica de fora: o libvips empacotado não traz o codec HEVC.
    private static readonly string[] ImageInputs = [".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".gif", ".avif"];

    // FR-23 e PRIV-30: desmarcado por padrão. Só nos formatos em que o Pizza consegue guardar os metadados.
    private static readonly ConverterOption Metadata = new(MetadataKey, "Manter metadados", MetadataStrip, [new(MetadataStrip, "Não"), new(MetadataKeep, "Sim")]);

    private static readonly ConverterOption[] Quality =
    [
        new("quality", "Qualidade", "75", [new("95", "Máxima"), new("85", "Alta"), new("75", "Equilibrada"), new("60", "Compacta")]),
        Metadata,
    ];

    public static IReadOnlyList<ConverterInfo> All { get; } =
    [
        new("image.jpg", ImageTab, "JPG", "Compacto", "jpg", ImagesExcept(".jpg", ".jpeg"), Quality),
        new(
            "image.png",
            ImageTab,
            "PNG",
            "Sem perda",
            "png",
            ImagesExcept(".png"),
            [
                new("resolution", "Resolução", "original", [new("original", "Original"), new("75", "75%"), new("50", "50%")], Minimum: 20),
                new("compression", "Compressão", "balanced", [new("fast", "Rápida"), new("balanced", "Equilibrada"), new("maximum", "Máxima")]),
                Metadata,
            ]),
        new("image.webp", ImageTab, "WEBP", "Para web", "webp", ImagesExcept(".webp"), Quality),
        new("image.avif", ImageTab, "AVIF", "Compacto moderno", "avif", ImagesExcept(".avif"), Quality),
        new(
            "image.pdf",
            ImageTab,
            "PDF",
            "Folha A4",
            "pdf",
            ImageInputs,
            [new("resolution", "Resolução", "original", [new("original", "Original"), new("150", "Escritório"), new("96", "Tela")])]),
        new("image.tiff", ImageTab, "TIFF", "Sem perda", "tiff", ImagesExcept(".tif", ".tiff"), []),
        new(
            "image.ico",
            ImageTab,
            "ICO",
            "Ícone",
            "ico",
            ImageInputs,
            [new("size", "Tamanho", "256", [new("256", "Grande"), new("128", "Médio"), new("48", "Pequeno")])]),
        new("image.gif", ImageTab, "GIF", "256 cores", "gif", ImagesExcept(".gif"), []),

        // ADR-033 e prancha 6A-1: a extensão não muda, então o nome ganha o sufixo.
        new("pdf.merge", PdfTab, "Juntar", "Vários em 1", "pdf", [".pdf"], [], Suffix: " - juntado", Merges: true),
        new(
            "pdf.split",
            PdfTab,
            "Dividir",
            "Por página",
            "pdf",
            [".pdf"],
            [new("mode", "Como dividir", "pages", [new("pages", "Cada página"), new("half", "Pela metade")])],
            Splits: true),
        new(
            "pdf.rotate",
            PdfTab,
            "Girar",
            "Todas as páginas",
            "pdf",
            [".pdf"],
            [new("rotation", "Girar páginas", "90", [new("90", "Direita"), new("180", "Ponta-cabeça"), new("270", "Esquerda")])],
            Suffix: " - girado"),

        // ADR-034: pelo motor de documentos isolado, só no Pizza Jumbo.
        new("document.pdf", DocsTab, "PDF", "Para enviar", "pdf", DocumentInputs, [], DocumentEngine: true),
    ];

    private static string[] ImagesExcept(params string[] own) => ImageInputs.Except(own).ToArray();

    public static ConverterInfo? Find(string id) => All.FirstOrDefault(converter => converter.Id == id);

    /// <summary>Completa as opções com os padrões. Devolve null se alguma estiver fora do esquema (SPECS §10.3).</summary>
    public static IReadOnlyDictionary<string, string>? ResolveOptions(ConverterInfo converter, IReadOnlyDictionary<string, string> given)
    {
        var resolved = new Dictionary<string, string>();
        foreach (var option in converter.Options)
        {
            var value = given.GetValueOrDefault(option.Key, option.Default);
            if (!option.Accepts(value))
            {
                return null;
            }

            resolved[option.Key] = value;
        }

        return given.Keys.All(resolved.ContainsKey) ? resolved : null;
    }
}
