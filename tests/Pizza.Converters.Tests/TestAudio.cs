using Pizza.Converters.Audio;

namespace Pizza.Converters.Tests;

/// <summary>WAV de teste gerado em memória: nenhum áudio de usuário entra no repositório.</summary>
internal static class TestAudio
{
    public static byte[] Sine(int seconds, int channels = 2, int rate = 44100) =>
        Wav(seconds, channels, rate, (i, _) => Math.Sin(2 * Math.PI * 440 * i / rate) * 0.4);

    /// <summary>Tom de 997 Hz (o da BS.1770) com a amplitude dada, de 0 a 1.</summary>
    public static byte[] Tone(double amplitude, int seconds = 3, int channels = 2, int rate = 48000) =>
        Wav(seconds, channels, rate, (i, _) => Math.Sin(2 * Math.PI * 997 * i / rate) * amplitude);

    /// <summary>Trechos de 1 s alternando forte (0,9) e fraco (0,05), para medir a compressão.</summary>
    public static byte[] Dynamic(int seconds = 4, int rate = 48000) =>
        Wav(seconds, 2, rate, (i, _) => Math.Sin(2 * Math.PI * 440 * i / rate) * (i / rate % 2 == 0 ? 0.9 : 0.05));

    /// <summary>Amostras PCM de 16 bits de um WAV, intercaladas, de -1 a 1.</summary>
    public static float[] Samples(byte[] wav)
    {
        var position = 12;
        while (position + 8 <= wav.Length)
        {
            var size = BitConverter.ToInt32(wav, position + 4);
            if (wav.AsSpan(position, 4).SequenceEqual("data"u8))
            {
                var count = Math.Min(size, wav.Length - position - 8) / 2;
                var samples = new float[count];
                for (var i = 0; i < count; i++)
                {
                    samples[i] = BitConverter.ToInt16(wav, position + 8 + (2 * i)) / 32768f;
                }

                return samples;
            }

            position += 8 + size + (size & 1);
        }

        throw new InvalidDataException("WAV sem bloco de dados");
    }

    public static byte[] Noise(int seconds)
    {
        var random = new Random(35);
        return Wav(seconds, 2, 44100, (_, _) => (random.NextDouble() * 2) - 1);
    }

    private static byte[] Wav(int seconds, int channels, int rate, Func<int, int, double> sample)
    {
        var frames = rate * seconds;
        var data = frames * channels * 2;
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);
        writer.Write("RIFF"u8);
        writer.Write(36 + data);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(rate);
        writer.Write(rate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(data);
        for (var i = 0; i < frames; i++)
        {
            for (var channel = 0; channel < channels; channel++)
            {
                writer.Write((short)(sample(i, channel) * short.MaxValue));
            }
        }

        writer.Flush();
        return memory.ToArray();
    }
}

/// <summary>Teste que precisa do Media Foundation. Num Windows sem ele (edição N, Server sem o recurso), aparece como ignorado, nunca como aprovado.</summary>
public sealed class MediaFactAttribute : FactAttribute
{
    public MediaFactAttribute()
    {
        if (!AudioFile.Available)
        {
            Skip = "Media Foundation ausente neste Windows (ADR-035).";
        }
    }
}

/// <inheritdoc cref="MediaFactAttribute"/>
public sealed class MediaTheoryAttribute : TheoryAttribute
{
    public MediaTheoryAttribute()
    {
        if (!AudioFile.Available)
        {
            Skip = "Media Foundation ausente neste Windows (ADR-035).";
        }
    }
}
