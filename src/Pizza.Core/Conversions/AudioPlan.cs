using System.Globalization;
using Pizza.Core.Ipc;

namespace Pizza.Core.Conversions;

/// <summary>Tamanho previsto da saída; <c>Exact</c> só no WAV, os outros levam "≈" (adendo do ADR-035).</summary>
public readonly record struct AudioSize(long Bytes, bool Exact)
{
    private const double Kilobyte = 1024;
    private const double Megabyte = 1024 * 1024;

    /// <summary>Como o Explorador mostra: KB abaixo de 1 MB, MB com uma casa e vírgula (o app roda sem culturas, NativeAOT).</summary>
    public string Text
    {
        get
        {
            var value = Bytes < Megabyte
                ? Math.Round(Bytes / Kilobyte).ToString("0", CultureInfo.InvariantCulture) + " KB"
                : (Bytes / Megabyte).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " MB";
            return Exact ? value : "≈ " + value;
        }
    }
}

/// <summary>
/// Taxa, canais e bits da saída de áudio a partir da origem e das opções. O Worker converte com esta regra e o
/// painel prevê o tamanho com ela, então os dois nunca divergem.
/// </summary>
public static class AudioPlan
{
    private const string Original = "original";
    private const int WaveHeader = 44;
    // Sem a medida do probe, uma média para música; com ela, a compressão real da amostra do arquivo.
    private const int LosslessFallbackPercent = 55;

    // Os codificadores de MP3 e AAC do Windows só trabalham em 44,1 e 48 kHz e em até dois canais.
    private static readonly int[] CompressedRates = [44100, 48000];

    public static (int Rate, int Channels, int Bits) Target(string format, IReadOnlyDictionary<string, string> options, int sourceRate, int sourceChannels, int sourceBits)
    {
        var lossless = format is "flac" or "wav" || options.GetValueOrDefault("bitrate") == "lossless";
        var compressed = !lossless;
        var rate = options["rate"] == Original ? sourceRate : int.Parse(options["rate"], CultureInfo.InvariantCulture);
        if (compressed && !CompressedRates.Contains(rate))
        {
            rate = 48000;
        }

        var channels = options["channels"] switch
        {
            "mono" => 1,
            "stereo" => 2,
            _ => Math.Max(1, sourceChannels),
        };
        if (format == "m4a" || compressed)
        {
            channels = Math.Min(channels, 2);
        }

        var depth = options.GetValueOrDefault("depth", Original);
        var bits = !lossless || (format == "m4a" && sourceBits != 24) ? 16
            // Origem com perda (MP3, AAC) não informa bits: 16 bastam, 24 só dobraria o arquivo sem ganho.
            : depth == Original ? (sourceBits is 24 or 32 ? 24 : 16)
            : int.Parse(depth, CultureInfo.InvariantCulture);
        return (rate, channels, bits);
    }

    public static AudioSize Estimate(string converterId, IReadOnlyDictionary<string, string> options, MediaInfo source)
    {
        var format = converterId["audio.".Length..];
        var (rate, channels, bits) = Target(format, options, source.SampleRate, source.Channels, source.Bits);
        var pcm = (long)Math.Round(source.DurationMs / 1000.0 * rate) * channels * (bits / 8);
        var lossless = source.LosslessPercent > 0 ? source.LosslessPercent : LosslessFallbackPercent;
        return format switch
        {
            "wav" => new AudioSize(WaveHeader + pcm, Exact: true),
            "flac" => new AudioSize(pcm * lossless / 100, Exact: false),
            _ when options["bitrate"] == "lossless" => new AudioSize(pcm * lossless / 100, Exact: false),
            _ => new AudioSize(long.Parse(options["bitrate"], CultureInfo.InvariantCulture) * 1000 * source.DurationMs / 1000 / 8, Exact: false),
        };
    }
}
