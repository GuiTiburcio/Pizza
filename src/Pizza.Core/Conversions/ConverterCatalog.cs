namespace Pizza.Core.Conversions;

/// <summary>Um valor possível de uma opção.</summary>
public sealed record OptionChoice(string Value, string Label);

/// <summary>Uma opção do painel. O padrão é sempre uma das escolhas.</summary>
/// <summary><c>Minimum</c>: a opção é uma régua de porcentagem (ADR-030), de Minimum a 100; 100 é "original".</summary>
public sealed record ConverterOption(string Key, string Label, string Default, IReadOnlyList<OptionChoice> Choices, int? Minimum = null, IReadOnlyList<OptionChoice>? QuickChoices = null)
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
    bool Splits = false)
{
    /// <summary>Sem opções, a conversão começa ao soltar (FR-21).</summary>
    public bool HasPanel => Options.Count > 0;

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
    public const int AudioTab = 3;

    private static readonly string[] AudioInputs = [".mp3", ".m4a", ".aac", ".flac", ".wav"];
    private static readonly ConverterOption AudioChannels = new("channels", "Canais", "original",
        [new("original", "Original"), new("mono", "Mono")]);
    private static readonly ConverterOption AudioRate = new("sampleRate", "Taxa de amostragem", "original",
        [new("original", "Original"), new("48000", "48 kHz"), new("44100", "44,1 kHz")]);
    private static readonly ConverterOption AudioDepth = new("depth", "Profundidade", "original",
        [new("original", "Original"), new("16", "16 bits"), new("24", "24 bits")]);

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

        // ADR-035: somente formatos de áudio; vídeo é validado/recusado também pelo conteúdo no Worker.
        new("audio.mp3", AudioTab, "MP3", "Compacto", "mp3", AudioInputs.Except([".mp3"]).ToArray(),
            [new("bitrate", "Qualidade (kbps)", "192", new[] { "96", "112", "128", "160", "192", "224", "256", "320" }.Select(v => new OptionChoice(v, v)).ToArray(),
                QuickChoices: [new("320", "Alta"), new("192", "Equilibrada"), new("128", "Compacta")]), AudioChannels, AudioRate]),
        new("audio.m4a", AudioTab, "M4A", "AAC ou ALAC", "m4a", AudioInputs.Except([".m4a"]).ToArray(),
            [new("bitrate", "Qualidade (kbps / ALAC)", "160", new[] { "64", "96", "128", "160", "192", "256", "320", "lossless" }.Select(v => new OptionChoice(v, v == "lossless" ? "ALAC" : v)).ToArray(),
                QuickChoices: [new("256", "Alta"), new("160", "Equilibrada"), new("96", "Compacta")]), AudioChannels, AudioRate]),
        new("audio.flac", AudioTab, "FLAC", "Sem perda", "flac", AudioInputs.Except([".flac"]).ToArray(),
            [AudioDepth, AudioChannels, AudioRate]),
        new("audio.wav", AudioTab, "WAV", "Sem compressão", "wav", AudioInputs.Except([".wav"]).ToArray(),
            [AudioDepth, AudioChannels, AudioRate]),

        // ADR-033 e prancha 6A-1: a extensão não muda, então o nome ganha o sufixo.
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
