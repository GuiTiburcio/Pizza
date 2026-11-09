namespace Pizza.Core.Conversions;

/// <summary>Um valor possível de uma opção.</summary>
public sealed record OptionChoice(string Value, string Label);

/// <summary>Uma opção do painel. O padrão é sempre uma das escolhas.</summary>
public sealed record ConverterOption(string Key, string Label, string Default, IReadOnlyList<OptionChoice> Choices);

/// <summary>O que o residente precisa saber de uma conversão, sem carregar nenhum decodificador.</summary>
public sealed record ConverterInfo(
    string Id,
    int Tab,
    string Label,
    string Caption,
    string Extension,
    IReadOnlyList<string> InputExtensions,
    IReadOnlyList<ConverterOption> Options);

/// <summary>Lista das conversões. O menu e o painel se montam daqui; a ordem é a das fatias (ADR-012).</summary>
public static class ConverterCatalog
{
    private const int ImageTab = 0;

    // SEC-45. HEIC fica de fora: o libvips empacotado não traz o codec HEVC.
    private static readonly string[] ImageInputs = [".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".gif", ".avif"];

    private static readonly ConverterOption[] Quality =
    [
        new("quality", "Qualidade", "75", [new("95", "Máxima"), new("85", "Alta"), new("75", "Equilibrada"), new("60", "Compacta")]),
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
                new("resolution", "Resolução", "original", [new("original", "Original"), new("75", "75%"), new("50", "50%")]),
                new("compression", "Compressão", "balanced", [new("fast", "Rápida"), new("balanced", "Equilibrada"), new("maximum", "Máxima")]),
            ]),
        new("image.webp", ImageTab, "WEBP", "Para web", "webp", ImagesExcept(".webp"), Quality),
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
            if (!option.Choices.Any(choice => choice.Value == value))
            {
                return null;
            }

            resolved[option.Key] = value;
        }

        return given.Keys.All(resolved.ContainsKey) ? resolved : null;
    }
}
