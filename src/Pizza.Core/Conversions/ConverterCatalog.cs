namespace Pizza.Core.Conversions;

/// <summary>Um valor possível de uma opção.</summary>
public sealed record OptionChoice(string Value, string Label);

/// <summary>Uma opção do painel. O padrão é sempre uma das escolhas.</summary>
/// <summary><c>Minimum</c>: a opção é uma régua de porcentagem (ADR-030), de Minimum a 100; 100 é "original".</summary>
/// <summary>
/// <c>Steps</c>: régua em degraus sobre as escolhas, na ordem delas (ADR-035). <c>Quick</c>: os atalhos da faixa de
/// níveis, quando não são todas as escolhas (prancha AU-2). <c>Advanced</c>: fica fora das linhas do painel, num
/// painel próprio, como o Volume (adendo do ADR-035, prancha AU-5).
/// </summary>
public sealed record ConverterOption(
    string Key,
    string Label,
    string Default,
    IReadOnlyList<OptionChoice> Choices,
    int? Minimum = null,
    bool Steps = false,
    IReadOnlyList<OptionChoice>? Quick = null,
    bool Advanced = false)
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

    /// <summary>
    /// Com várias saídas, o sufixo de cada parte; null quando há uma saída só. O Dividir escolhe pelo modo; o
    /// CSV, que não tem painel, numera as planilhas.
    /// </summary>
    public Func<int, int, string>? PartSuffix(IReadOnlyDictionary<string, string> options) =>
        Splits ? (part, count) => Output.OutputNaming.PartSuffix(Options.Count == 0 ? "sheets" : options.GetValueOrDefault("mode", Options[0].Default), part, count) : null;
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
    public const int AudioTab = 3;

    // ADR-034: o que o motor de documentos do Pizza Jumbo abre (Writer, Calc e Impress, formatos atuais e antigos).
    private static readonly string[] DocumentInputs = [".docx", ".doc", ".odt", ".rtf", ".xlsx", ".xls", ".ods", ".pptx", ".ppt", ".odp"];

    /// <summary>ADR-034: o motor de documentos existe só no Pizza Jumbo; o residente liga isto ao abrir.</summary>
    public static bool DocumentEngineAvailable { get; set; }

    // SEC-45. HEIC fica de fora: o libvips empacotado não traz o codec HEVC.
    private static readonly string[] ImageInputs = [".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".gif", ".avif"];

    // FR-23 e PRIV-30: desmarcado por padrão. Só nos formatos em que o Pizza consegue guardar os metadados.
    private static readonly ConverterOption Metadata = new(MetadataKey, "Manter metadados", MetadataStrip, [new(MetadataStrip, "Não"), new(MetadataKeep, "Sim")]);

    // ADR-035: o que o Media Foundation do Windows lê; o formato real é conferido pelo conteúdo no Worker.
    private static readonly string[] AudioInputs = [".mp3", ".m4a", ".aac", ".flac", ".wav"];

    // Valores que os codificadores do Windows aceitam (prova de 12/11/2026, ADR-035).
    private static readonly ConverterOption Mp3Bitrate = new(
        "bitrate", "Qualidade", "192", Kbps(96, 112, 128, 160, 192, 224, 256, 320), Steps: true,
        Quick: [new("320", "Alta"), new("192", "Equilibrada"), new("128", "Compacta")]);

    private static readonly ConverterOption M4aBitrate = new(
        "bitrate", "Qualidade", "160", [.. Kbps(64, 96, 128, 160, 192, 256, 320), new("lossless", "Sem perda")], Steps: true,
        Quick: [new("256", "Alta"), new("160", "Equilibrada"), new("96", "Compacta")]);

    private static readonly ConverterOption Depth = new("depth", "Profundidade", "original", [new("original", "Original"), new("16", "16 bits"), new("24", "24 bits")]);

    private static readonly ConverterOption Channels = new("channels", "Canais", "original", [new("original", "Original"), new("stereo", "Estéreo"), new("mono", "Mono")]);

    private static readonly ConverterOption Rate = new("rate", "Taxa de amostragem", "original", [new("original", "Original"), new("48000", "48 kHz"), new("44100", "44,1 kHz")]);

    // Adendo do ADR-035: Volume, aplicado nesta ordem no Worker. "off" é o Original (nada é aplicado).
    public const string Off = "off";

    private static readonly ConverterOption[] Volume =
    [
        new("gain", "Amplificar", Off, Range(-12, 12, "dB"), Steps: true, Advanced: true),
        new("compress", "Comprimir · limiar", Off, Range(-40, 0, "dB"), Steps: true, Advanced: true),
        new("ratio", "Razão", "4", [new("2", "2:1"), new("4", "4:1"), new("8", "8:1")], Advanced: true),
        new("normalize", "Normalizar", Off, Range(-23, -9, "LUFS"), Steps: true, Advanced: true),
        new("limit", "Limitar · teto", Off, Range(-6, 0, "dB"), Steps: true, Advanced: true),
    ];

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

        // ADR-034, Pizza Brotinho: sem diagramação, no Worker de sempre; cada planilha vira um CSV.
        new("document.txt", DocsTab, "TXT", "Só o texto", "txt", [".docx"], []),
        new("document.csv", DocsTab, "CSV", "Tabela simples", "csv", [".xlsx"], [], Splits: true),

        // ADR-035: pelo Media Foundation, no Worker isolado. A fatia do próprio formato não aparece.
        new("audio.mp3", AudioTab, "MP3", "Toca em tudo", "mp3", AudioExcept(".mp3"), [Mp3Bitrate, Channels, Rate, .. Volume]),
        new("audio.m4a", AudioTab, "M4A", "Leve", "m4a", AudioExcept(".m4a"), [M4aBitrate, Channels, Rate, .. Volume]),
        new("audio.flac", AudioTab, "FLAC", "Sem perda", "flac", AudioExcept(".flac"), [Depth, Channels, Rate, .. Volume]),
        new("audio.wav", AudioTab, "WAV", "Sem compressão", "wav", AudioExcept(".wav"), [Depth, Channels, Rate, .. Volume]),
    ];

    private static string[] ImagesExcept(params string[] own) => ImageInputs.Except(own).ToArray();

    private static string[] AudioExcept(string own) => AudioInputs.Except([own]).ToArray();

    /// <summary>"Desligado" e os inteiros de <paramref name="from"/> a <paramref name="to"/>, com sinal e sinal de menos tipográfico no rótulo.</summary>
    private static OptionChoice[] Range(int from, int to, string unit) =>
    [
        new(Off, "Desligado"),
        .. Enumerable.Range(from, to - from + 1).Select(value => new OptionChoice(
            value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"{(value > 0 ? "+" : value < 0 ? "−" : "")}{Math.Abs(value)} {unit}")),
    ];

    private static OptionChoice[] Kbps(params int[] values) =>
        values.Select(value => new OptionChoice(value.ToString(System.Globalization.CultureInfo.InvariantCulture), $"{value} kbps")).ToArray();

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
