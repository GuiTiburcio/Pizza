using System.Globalization;

namespace Pizza.Converters.Audio;

/// <summary>
/// Volume (adendo do ADR-035): Amplificar → Comprimir → Normalizar → Limitar, sobre amostras intercaladas de -1 a 1.
/// Cada instância guarda o estado de uma passada; a segunda passada usa uma nova.
/// </summary>
internal sealed class VolumeChain
{
    private const string Off = "off";
    private const double AttackSeconds = 0.010;
    private const double CompressorReleaseSeconds = 0.100;
    private const double LimiterReleaseSeconds = 0.050;

    private readonly double _gain;
    private readonly double? _threshold;
    private readonly double _slope;
    private readonly double? _ceiling;
    private readonly double _attack;
    private readonly double _compressorRelease;
    private readonly double _limiterRelease;
    private double _envelope;
    private double _limiterGain = 1;

    public VolumeChain(IReadOnlyDictionary<string, string> options, int rate)
    {
        _gain = Number(options, "gain") is { } gain ? Linear(gain) : 1;
        _threshold = Number(options, "compress");
        _slope = 1 - (1 / Number(options, "ratio").GetValueOrDefault(1));
        _ceiling = Number(options, "limit") is { } limit ? Linear(limit) : null;
        _attack = Math.Exp(-1 / (AttackSeconds * rate));
        _compressorRelease = Math.Exp(-1 / (CompressorReleaseSeconds * rate));
        _limiterRelease = Math.Exp(-1 / (LimiterReleaseSeconds * rate));
        Target = Number(options, "normalize");
        Active = _gain != 1 || _threshold is not null || _ceiling is not null || Target is not null;
    }

    /// <summary>Algum efeito ligado; sem nenhum, as amostras passam direto, sem conversão.</summary>
    public bool Active { get; }

    /// <summary>Alvo do Normalizar em LUFS; quando há alvo, a conversão mede antes numa primeira passada.</summary>
    public double? Target { get; }

    /// <summary>Ganho do Normalizar, calculado depois da medição.</summary>
    public double Normalize { get; set; } = 1;

    /// <summary>Só Amplificar e Comprimir: o que a medição do Normalizar precisa ouvir.</summary>
    public void Before(Span<float> interleaved, int channels)
    {
        for (var index = 0; index + channels <= interleaved.Length; index += channels)
        {
            var frame = interleaved.Slice(index, channels);
            var peak = 0.0;
            foreach (ref var sample in frame)
            {
                sample = (float)(sample * _gain);
                peak = Math.Max(peak, Math.Abs(sample));
            }

            if (_threshold is { } threshold)
            {
                // Detector de pico ligado nos canais, com ataque e soltura; reduz o que passa do limiar pela razão.
                _envelope = peak > _envelope ? (_attack * _envelope) + ((1 - _attack) * peak) : (_compressorRelease * _envelope) + ((1 - _compressorRelease) * peak);
                var over = Decibels(_envelope) - threshold;
                if (over > 0)
                {
                    var reduction = (float)Linear(-over * _slope);
                    foreach (ref var sample in frame)
                    {
                        sample *= reduction;
                    }
                }
            }
        }
    }

    /// <summary>A cadeia inteira.</summary>
    public void Apply(Span<float> interleaved, int channels)
    {
        Before(interleaved, channels);
        for (var index = 0; index + channels <= interleaved.Length; index += channels)
        {
            var frame = interleaved.Slice(index, channels);
            var peak = 0.0;
            foreach (ref var sample in frame)
            {
                sample = (float)(sample * Normalize);
                peak = Math.Max(peak, Math.Abs(sample));
            }

            if (_ceiling is { } ceiling)
            {
                // Ataque instantâneo: o ganho cai na hora o bastante para o pico caber no teto; volta em 50 ms.
                var needed = peak > ceiling ? ceiling / peak : 1;
                _limiterGain = Math.Min(needed, (_limiterRelease * _limiterGain) + (1 - _limiterRelease));
                foreach (ref var sample in frame)
                {
                    sample = (float)(sample * _limiterGain);
                }
            }
        }
    }

    private static double? Number(IReadOnlyDictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var value) && value != Off ? double.Parse(value, CultureInfo.InvariantCulture) : null;

    private static double Linear(double decibels) => Math.Pow(10, decibels / 20);

    private static double Decibels(double linear) => linear <= 0 ? double.NegativeInfinity : 20 * Math.Log10(linear);
}
