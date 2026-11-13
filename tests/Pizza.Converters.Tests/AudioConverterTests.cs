using System.Text;
using Pizza.Converters.Audio;

namespace Pizza.Converters.Tests;

/// <summary>ADR-035: conversões de áudio pelo Media Foundation, com o áudio de teste gerado aqui (sem arquivo de usuário).</summary>
public class AudioConverterTests
{
    [MediaFact]
    public void Wav_vira_cada_formato_com_a_assinatura_certa_e_a_mesma_duracao()
    {
        var wav = TestAudio.Sine(seconds: 2);

        foreach (var (id, signature) in new[] { ("audio.mp3", "mp3"), ("audio.m4a", "m4a"), ("audio.flac", "flac") })
        {
            var (error, output) = Run(id, wav);

            Assert.Null(error);
            Assert.True(HasSignature(output, signature), id);
            var info = Inspect(output);
            Assert.InRange(info.DurationMs, 1950, 2100);
            Assert.Equal(2, info.Channels);
        }
    }

    [MediaFact]
    public void Cada_formato_volta_para_wav()
    {
        foreach (var id in new[] { "audio.mp3", "audio.m4a", "audio.flac" })
        {
            var (_, encoded) = Run(id, TestAudio.Sine(seconds: 1));

            var (error, wav) = Run("audio.wav", encoded);

            Assert.Null(error);
            Assert.True(HasSignature(wav, "wav"), id);
            Assert.InRange(Inspect(wav).DurationMs, 950, 1100);
        }
    }

    [MediaFact]
    public void Qualidade_maior_gera_arquivo_maior()
    {
        var wav = TestAudio.Noise(seconds: 2);

        var (_, high) = Run("audio.mp3", wav, ("bitrate", "320"));
        var (_, low) = Run("audio.mp3", wav, ("bitrate", "128"));
        var (_, aac) = Run("audio.m4a", wav, ("bitrate", "96"));
        var (_, lossless) = Run("audio.m4a", wav, ("bitrate", "lossless"));

        Assert.True(high.Length > low.Length * 2);
        Assert.True(lossless.Length > aac.Length * 3);
    }

    [MediaFact]
    public void Canais_taxa_e_profundidade_seguem_as_opcoes()
    {
        var wav = TestAudio.Sine(seconds: 1);

        Assert.Equal(1, Inspect(Run("audio.mp3", wav, ("channels", "mono")).Output).Channels);
        Assert.Equal(48000, Inspect(Run("audio.wav", wav, ("rate", "48000")).Output).SampleRate);
        Assert.Equal(24, Inspect(Run("audio.flac", wav, ("depth", "24")).Output).Bits);
        Assert.Equal(2, Inspect(Run("audio.wav", TestAudio.Sine(seconds: 1, channels: 1), ("channels", "stereo")).Output).Channels);
    }

    [MediaFact]
    public void Estereo_mixa_51_em_dois_canais()
    {
        var (error, output) = Run("audio.wav", TestAudio.Sine(seconds: 1, channels: 6), ("channels", "stereo"));

        Assert.Null(error);
        Assert.Equal(2, Inspect(output).Channels);
    }

    [MediaFact]
    public void Origem_de_96_kHz_vai_para_48_kHz_no_MP3()
    {
        // ADR-035: MP3 e AAC só aceitam 44,1 e 48 kHz; em "Original", outra taxa vira 48 kHz.
        var (error, output) = Run("audio.mp3", TestAudio.Sine(seconds: 1, rate: 96000));

        Assert.Null(error);
        Assert.Equal(48000, Inspect(output).SampleRate);
    }

    [MediaTheory]
    [InlineData("isto não é áudio")]
    [InlineData("\u0089PNG\r\n\u001a\n00000000")]
    public void Arquivo_que_nao_e_audio_e_recusado(string content)
    {
        // Equivalente da SEC-45 para áudio: o formato é conferido pelo conteúdo, não pela extensão.
        var (error, output) = Run("audio.mp3", Encoding.Latin1.GetBytes(content));

        Assert.Equal("UNSUPPORTED", error);
        Assert.Empty(output);
    }

    [MediaFact]
    public void Wav_cortado_no_meio_nao_trava()
    {
        var wav = TestAudio.Sine(seconds: 1);

        var (error, _) = Run("audio.mp3", wav[..(wav.Length / 3)]);

        Assert.True(error is null or "CORRUPTED", error);
    }

    [MediaFact]
    public void Progresso_so_avanca_e_chega_a_100()
    {
        var reported = new List<int>();

        Run("audio.mp3", TestAudio.Sine(seconds: 3), progress: new Collector(reported));

        Assert.NotEmpty(reported);
        Assert.Equal(reported.Order().ToList(), reported);
        Assert.Equal(100, reported[^1]);
    }

    private static (string? Error, byte[] Output) Run(string id, byte[] source, params (string Key, string Value)[] options) =>
        Run(id, source, null, options);

    private static (string? Error, byte[] Output) Run(string id, byte[] source, IProgress<int>? progress, params (string Key, string Value)[] options)
    {
        var info = Core.Conversions.ConverterCatalog.Find(id)!;
        var resolved = Core.Conversions.ConverterCatalog.ResolveOptions(info, options.ToDictionary(o => o.Key, o => o.Value))!;
        using var input = new MemoryStream(source);
        using var output = new MemoryStream();
        var error = ConverterRegistry.Find(id)!.Convert(input, output, resolved, progress);
        return (error, output.ToArray());
    }

    private static AudioInfo Inspect(byte[] audio)
    {
        using var stream = new MemoryStream(audio);
        return AudioFile.Inspect(stream)!.Value;
    }

    private static bool HasSignature(byte[] data, string format) => format switch
    {
        "mp3" => data.AsSpan().StartsWith("ID3"u8) || (data[0] == 0xFF && (data[1] & 0xE0) == 0xE0),
        "m4a" => data.AsSpan(4, 4).SequenceEqual("ftyp"u8),
        "flac" => data.AsSpan().StartsWith("fLaC"u8),
        _ => data.AsSpan().StartsWith("RIFF"u8) && data.AsSpan(8, 4).SequenceEqual("WAVE"u8),
    };

    private sealed class Collector(List<int> values) : IProgress<int>
    {
        public void Report(int value) => values.Add(value);
    }
}
