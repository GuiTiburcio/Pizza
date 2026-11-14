namespace Pizza.Converters.Audio;

/// <summary>
/// Volume percebido integrado, em LUFS, pela ITU-R BS.1770-4: filtro K, blocos de 400 ms com passo de 100 ms,
/// porta absoluta em -70 LUFS e porta relativa 10 LU abaixo. Usado pelo Normalizar (adendo do ADR-035).
/// </summary>
internal sealed class Loudness
{
    private const double AbsoluteGate = -70;
    private const double RelativeGate = -10;
    private const double Offset = -0.691;
    private const int SubBlocksPerBlock = 4;

    private readonly int _channels;
    private readonly double[] _weights;
    private readonly Biquad[] _shelf;
    private readonly Biquad[] _highPass;
    private readonly int _subBlockFrames;
    private readonly double[] _sums;
    private readonly Queue<double> _recent = new();
    private readonly List<double> _blocks = [];
    private int _frames;

    public Loudness(int channels, int rate)
    {
        _channels = channels;

        // Pesos da norma: 1,0 na frente e no centro, 1,41 nos surround; o LFE do 5.1 fica de fora.
        _weights = Enumerable.Range(0, channels).Select(channel => channels == 6 ? channel switch { 3 => 0, 4 or 5 => 1.41, _ => 1.0 } : 1.0).ToArray();
        _shelf = Enumerable.Range(0, channels).Select(_ => Biquad.Shelf(rate)).ToArray();
        _highPass = Enumerable.Range(0, channels).Select(_ => Biquad.HighPass(rate)).ToArray();
        _subBlockFrames = rate / 10;
        _sums = new double[channels];
    }

    /// <summary>Volume integrado de um trecho inteiro; -∞ quando não há nada acima da porta absoluta.</summary>
    public static double Integrated(ReadOnlySpan<float> interleaved, int channels, int rate)
    {
        var meter = new Loudness(channels, rate);
        meter.Add(interleaved);
        return meter.Result;
    }

    /// <summary>Acrescenta amostras intercaladas, de -1 a 1.</summary>
    public void Add(ReadOnlySpan<float> interleaved)
    {
        for (var index = 0; index + _channels <= interleaved.Length; index += _channels)
        {
            for (var channel = 0; channel < _channels; channel++)
            {
                var filtered = _highPass[channel].Next(_shelf[channel].Next(interleaved[index + channel]));
                _sums[channel] += filtered * filtered;
            }

            if (++_frames == _subBlockFrames)
            {
                CloseSubBlock();
            }
        }
    }

    public double Result
    {
        get
        {
            var gated = _blocks.Where(energy => Lufs(energy) > AbsoluteGate).ToArray();
            if (gated.Length == 0)
            {
                return double.NegativeInfinity;
            }

            var threshold = Lufs(gated.Average()) + RelativeGate;
            var kept = gated.Where(energy => Lufs(energy) > threshold).ToArray();
            return Lufs(kept.Average());
        }
    }

    private static double Lufs(double energy) => Offset + (10 * Math.Log10(energy));

    private void CloseSubBlock()
    {
        var energy = 0.0;
        for (var channel = 0; channel < _channels; channel++)
        {
            energy += _weights[channel] * _sums[channel] / _subBlockFrames;
            _sums[channel] = 0;
        }

        _frames = 0;
        _recent.Enqueue(energy);
        if (_recent.Count > SubBlocksPerBlock)
        {
            _recent.Dequeue();
        }

        if (_recent.Count == SubBlocksPerBlock)
        {
            _blocks.Add(_recent.Average());
        }
    }

    /// <summary>Filtro de segunda ordem; coeficientes do filtro K para qualquer taxa (os mesmos da libebur128).</summary>
    private sealed class Biquad(double b0, double b1, double b2, double a1, double a2)
    {
        private double _x1, _x2, _y1, _y2;

        public static Biquad Shelf(int rate)
        {
            const double f0 = 1681.974450955533, gain = 3.999843853973347, q = 0.7071752369554196;
            var k = Math.Tan(Math.PI * f0 / rate);
            var vh = Math.Pow(10, gain / 20);
            var vb = Math.Pow(vh, 0.4996667741545416);
            var a0 = 1 + (k / q) + (k * k);
            return new Biquad((vh + (vb * k / q) + (k * k)) / a0, 2 * ((k * k) - vh) / a0, (vh - (vb * k / q) + (k * k)) / a0, 2 * ((k * k) - 1) / a0, (1 - (k / q) + (k * k)) / a0);
        }

        public static Biquad HighPass(int rate)
        {
            const double f0 = 38.13547087602444, q = 0.5003270373238773;
            var k = Math.Tan(Math.PI * f0 / rate);
            var a0 = 1 + (k / q) + (k * k);
            return new Biquad(1, -2, 1, 2 * ((k * k) - 1) / a0, (1 - (k / q) + (k * k)) / a0);
        }

        public double Next(double x)
        {
            var y = (b0 * x) + (b1 * _x1) + (b2 * _x2) - (a1 * _y1) - (a2 * _y2);
            (_x2, _x1, _y2, _y1) = (_x1, x, _y1, y);
            return y;
        }
    }
}
