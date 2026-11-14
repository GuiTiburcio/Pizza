using Pizza.Converters.Audio;
using Pizza.Core.Conversions;

namespace Pizza.Converters.Tests;

/// <summary>Adendo do ADR-035: o tamanho previsto no painel fica perto do arquivo que a conversão grava.</summary>
public class AudioSizeTests
{
    public static TheoryData<string, string> Cases => new()
    {
        { "tom", "audio.flac" }, { "ruido", "audio.flac" }, { "tom", "audio.wav" }, { "ruido", "audio.m4a" }, { "tom", "audio.mp3" },
    };

    [MediaTheory]
    [MemberData(nameof(Cases))]
    public void Previsto_fica_a_menos_de_15_por_cento_do_real(string content, string id)
    {
        // Sem perda varia com o conteúdo: o probe mede a compressão numa amostra do próprio arquivo.
        var source = content == "tom" ? TestAudio.Sine(seconds: 8) : TestAudio.Noise(seconds: 8);
        var info = AudioConverter.Probe(new MemoryStream(source))!;
        var converter = ConverterCatalog.Find(id)!;
        var options = ConverterCatalog.ResolveOptions(converter, new Dictionary<string, string>())!;
        using var output = new MemoryStream();
        Assert.Null(ConverterRegistry.Find(id)!.Convert(new MemoryStream(source), output, options));

        var estimate = AudioPlan.Estimate(id, options, info).Bytes;

        Assert.InRange(estimate, output.Length * 0.85, output.Length * 1.15);
    }

    [MediaFact]
    public void Probe_mede_a_compressao_sem_perda_da_amostra()
    {
        var simple = AudioConverter.Probe(new MemoryStream(TestAudio.Sine(seconds: 8)))!;
        var noisy = AudioConverter.Probe(new MemoryStream(TestAudio.Noise(seconds: 8)))!;

        Assert.InRange(simple.LosslessPercent, 1, 60);
        Assert.InRange(noisy.LosslessPercent, 80, 100);
    }
}
