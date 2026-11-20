using System.Globalization;

namespace Pizza.Core.Conversions;

/// <summary>ADR-035: sugestão numérica do próprio catálogo, nunca substituição do modo sem perda.</summary>
public static class BitrateOffer
{
    public const string Required = "QUALITY_UNAVAILABLE";

    public static bool IsValid(string converterId, IReadOnlyDictionary<string, string> options, int suggested)
    {
        if (converterId is not ("audio.mp3" or "audio.m4a"))
        {
            return false;
        }

        var option = ConverterCatalog.Find(converterId)!.Options.First(item => item.Key == "bitrate");
        var chosen = options.GetValueOrDefault("bitrate", option.Default);
        return option.Accepts(chosen)
            && int.TryParse(chosen, NumberStyles.None, CultureInfo.InvariantCulture, out var requested)
            && suggested < requested
            && option.Choices.Any(choice => choice.Value == suggested.ToString(CultureInfo.InvariantCulture));
    }

    public static IReadOnlyDictionary<string, string>? Accept(string converterId, IReadOnlyDictionary<string, string> options, int suggested) =>
        IsValid(converterId, options, suggested)
            ? new Dictionary<string, string>(options) { ["bitrate"] = suggested.ToString(CultureInfo.InvariantCulture) }
            : null;

    public static string Message(string converterId, IReadOnlyDictionary<string, string> options, int suggested)
    {
        var requested = options.GetValueOrDefault("bitrate", ConverterCatalog.Find(converterId)!.Options.First(item => item.Key == "bitrate").Default);
        return $"{requested} kbps não aceitos. Usar {suggested.ToString(CultureInfo.InvariantCulture)} kbps?";
    }
}
