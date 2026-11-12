using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Pizza.Converters.Audio;

namespace Pizza.Converters.Tests;

/// <summary>TST-02 / TST-10 / ADR-035: codecs reais com áudio sintético, sem skips de disponibilidade.</summary>
public sealed class AudioConversionTests
{
    private static IReadOnlyDictionary<string, string> Options(string id)
    {
        var options = new Dictionary<string, string> { ["channels"] = "original", ["sampleRate"] = "44100" };
        if (id is "audio.flac" or "audio.wav") options["depth"] = "16";
        return options;
    }

    [Theory]
    [InlineData("audio.mp3")]
    [InlineData("audio.m4a")]
    [InlineData("audio.flac")]
    [InlineData("audio.wav")]
    public void Formato_real_volta_a_pcm_com_duracao_canais_e_sinal_preservados(string id)
    {
        var source = AudioSamples.Wave();
        var before = SHA256.HashData(source);
        var encoded = Convert(id, source);
        AssertSignature(id, encoded);
        var decoded = AudioSamples.ReadWave(Convert("audio.wav", encoded));

        Assert.Equal(before, SHA256.HashData(source));
        Assert.Equal((2, AudioSamples.Rate, 16), (decoded.Channels, decoded.Rate, decoded.Bits));
        var seconds = decoded.Data.Length / (decoded.Rate * decoded.Channels * (decoded.Bits / 8.0));
        // MP3/AAC têm atraso e preenchimento de codificação; não fingir identidade de amostras.
        Assert.InRange(seconds, 0.15, 0.36);
        double energy = 0;
        for (var offset = 0; offset < decoded.Data.Length; offset += 2)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(decoded.Data.AsSpan(offset)) / 32768.0;
            energy += sample * sample;
        }
        Assert.InRange(Math.Sqrt(energy / (decoded.Data.Length / 2)), 0.03, 0.35);
    }

    [Fact]
    public void Flac_de_16_bits_volta_sem_perder_as_amostras_pcm()
    {
        var source = AudioSamples.Wave();
        var original = AudioSamples.ReadWave(source);
        var encoded = Convert("audio.flac", source);
        var decoded = AudioSamples.ReadWave(Convert("audio.wav", encoded));
        Assert.Equal(original.Data, decoded.Data);
    }

    [Fact]
    public void Silencio_nao_vira_ruido_no_formato_sem_perda()
    {
        var decoded = AudioSamples.ReadWave(Convert("audio.wav", Convert("audio.flac", AudioSamples.Wave(silence: true))));
        Assert.All(decoded.Data, value => Assert.Equal((byte)0, value));
    }

    [Theory]
    [InlineData("lixo")]
    [InlineData("asf")]
    [InlineData("avi")]
    public void Conteudo_fora_da_lista_e_recusado_sem_ser_audio(string kind)
    {
        var source = kind switch
        {
            "asf" => new Guid("75b22630-668e-11cf-a6d9-00aa0062ce6c").ToByteArray().Concat(new byte[64]).ToArray(),
            "avi" => "RIFF\x40\0\0\0AVI "u8.ToArray().Concat(new byte[64]).ToArray(),
            _ => Encoding.ASCII.GetBytes("Isto nao e audio; extensao nao valida o conteudo."),
        };
        using var input = new MemoryStream(source, writable: false);
        using var output = new MemoryStream();
        Assert.Equal("UNSUPPORTED", new AudioConverter("audio.mp3").Convert(input, output, Options("audio.mp3")));
    }

    private static byte[] Convert(string id, byte[] bytes)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using var output = new MemoryStream();
        var progress = new Percentages();
        var error = new AudioConverter(id).Convert(input, output, Options(id), progress);
        Assert.True(error is null, $"{id}: {error ?? "ok"}; o CI deve fornecer Media Foundation, sem ignorar codecs ausentes.");
        Assert.NotEmpty(progress.Values);
        Assert.All(progress.Values, value => Assert.InRange(value, 0, 100));
        Assert.Equal(progress.Values.Order().ToArray(), progress.Values.ToArray());
        return output.ToArray();
    }

    private static void AssertSignature(string id, byte[] bytes)
    {
        Assert.True(bytes.Length >= 12);
        switch (id)
        {
            case "audio.mp3":
                Assert.True(bytes.AsSpan(0, 3).SequenceEqual("ID3"u8) || (bytes[0] == 0xff && (bytes[1] & 0xe0) == 0xe0));
                break;
            case "audio.m4a": Assert.Equal("ftyp", Encoding.ASCII.GetString(bytes, 4, 4)); break;
            case "audio.flac": Assert.Equal("fLaC", Encoding.ASCII.GetString(bytes, 0, 4)); break;
            case "audio.wav": Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4)); break;
        }
    }

    private sealed class Percentages : IProgress<int>
    {
        public List<int> Values { get; } = [];
        public void Report(int value) => Values.Add(value);
    }
}
