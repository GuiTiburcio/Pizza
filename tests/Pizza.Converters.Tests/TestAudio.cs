using Pizza.Converters.Audio;

namespace Pizza.Converters.Tests;

/// <summary>WAV de teste gerado em memória: nenhum áudio de usuário entra no repositório.</summary>
internal static class TestAudio
{
    public static byte[] Sine(int seconds, int channels = 2, int rate = 44100) =>
        Wav(seconds, channels, rate, (i, _) => Math.Sin(2 * Math.PI * 440 * i / rate) * 0.4);

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
