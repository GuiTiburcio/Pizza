using System.Globalization;

namespace Pizza.Core.Output;

/// <summary>Marca "veio da internet" (MotW). Só a zona é copiada; as URLs de origem, não (SEC-35).</summary>
public static class ZoneIdentifier
{
    private const string Prefix = "ZoneId=";
    private const int MaxZone = 4;
    internal const int MaxBytes = 64 * 1024;

    /// <summary>SEC-35/40: conta bytes durante a leitura, inclusive se o ADS crescer depois de aberto.</summary>
    internal static int? Read(Stream source)
    {
        var bytes = new byte[MaxBytes + 1];
        var count = source.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
        if (count > MaxBytes)
        {
            throw new ConversionLimitException();
        }

        // Mantém UTF-8 e a detecção de BOM de ReadAllText, com texto de tamanho já limitado.
        using var reader = new StreamReader(new MemoryStream(bytes, 0, count), detectEncodingFromByteOrderMarks: true);
        return ParseZone(reader.ReadToEnd());
    }

    public static int? ParseZone(string content)
    {
        foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var zone)
                && zone <= MaxZone)
            {
                return zone;
            }
        }

        return null;
    }

    /// <summary>Juntar (SEC-35): a zona mais restritiva entre as origens, ou null se nenhuma tiver marca.</summary>
    public static int? MostRestrictive(IEnumerable<int?> zones) => zones.Max();

    public static string Format(int zone) => $"[ZoneTransfer]\r\nZoneId={zone.ToString(CultureInfo.InvariantCulture)}\r\n";
}
