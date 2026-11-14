using Pizza.Converters.Audio;

namespace Pizza.Converters.Tests;

/// <summary>
/// Adendo do ADR-035: Amplificar, Comprimir, Normalizar e Limitar, aplicados nesta ordem no Worker. Cada efeito é
/// conferido medindo o WAV de saída; o áudio de teste é gerado aqui.
/// </summary>
public class AudioVolumeTests
{
    [Fact]
    public void Medidor_BS1770_da_0_LUFS_para_tom_de_997_Hz_a_0_dBFS_nos_dois_canais()
    {
        // ITU-R BS.1770-4: 997 Hz a 0 dBFS num canal mede -3,01 LKFS; nos dois canais, 0,0.
        var samples = TestAudio.Samples(TestAudio.Tone(amplitude: 1.0));

        Assert.Equal(0.0, Loudness.Integrated(samples, channels: 2, rate: 48000), 1);
        Assert.Equal(-3.0, Loudness.Integrated(samples.Where((_, i) => i % 2 == 0).ToArray(), channels: 1, rate: 48000), 1);
    }

    [Fact]
    public void Silencio_nao_tem_volume_medido()
    {
        Assert.True(double.IsNegativeInfinity(Loudness.Integrated(new float[96000], channels: 2, rate: 48000)));
    }

    [MediaFact]
    public void Sem_efeito_ligado_o_som_nao_muda()
    {
        var source = TestAudio.Tone(amplitude: 0.5, seconds: 1);

        var output = Volume(source);

        Assert.Equal(Peak(TestAudio.Samples(source)), Peak(output), 2);
    }

    [MediaFact]
    public void Amplificar_6_dB_dobra_o_pico()
    {
        var output = Volume(TestAudio.Tone(amplitude: 0.25, seconds: 1), ("gain", "6"));

        Assert.InRange(Peak(output), 0.49, 0.51);
    }

    [MediaFact]
    public void Normalizar_leva_ao_alvo_em_LUFS()
    {
        var output = Volume(TestAudio.Tone(amplitude: 0.05), ("normalize", "-14"));

        Assert.InRange(Loudness.Integrated(output, channels: 2, rate: 48000), -14.5, -13.5);
    }

    [MediaFact]
    public void Limitar_nao_deixa_nenhuma_amostra_acima_do_teto()
    {
        // +12 dB num tom de 0,5 estouraria; com o limitador em -1 dB, nada passa de 0,891.
        var output = Volume(TestAudio.Tone(amplitude: 0.5, seconds: 1), ("gain", "12"), ("limit", "-1"));

        Assert.True(Peak(output) <= Math.Pow(10, -1 / 20.0) + 0.001, $"pico {Peak(output)}");
    }

    [MediaFact]
    public void Comprimir_aproxima_os_trechos_fortes_dos_fracos()
    {
        var source = TestAudio.Dynamic();
        var before = Range(TestAudio.Samples(source));

        var after = Range(Volume(source, ("compress", "-30"), ("ratio", "4")));

        Assert.True(after < before - 10, $"antes {before:F1} dB, depois {after:F1} dB");
    }

    [MediaFact]
    public void Normalizar_depois_de_comprimir_ainda_acerta_o_alvo()
    {
        var output = Volume(TestAudio.Dynamic(), ("compress", "-30"), ("ratio", "8"), ("normalize", "-16"), ("limit", "-1"));

        Assert.InRange(Loudness.Integrated(output, channels: 2, rate: 48000), -16.7, -15.3);
        Assert.True(Peak(output) <= Math.Pow(10, -1 / 20.0) + 0.001);
    }

    [MediaFact]
    public void Volume_tambem_vale_em_24_bits_e_em_formato_com_perda()
    {
        var source = TestAudio.Tone(amplitude: 0.25, seconds: 1);

        foreach (var (id, extra) in new[] { ("audio.flac", ("depth", "24")), ("audio.mp3", ("bitrate", "320")) })
        {
            var info = Core.Conversions.ConverterCatalog.Find(id)!;
            var options = Core.Conversions.ConverterCatalog.ResolveOptions(info, new Dictionary<string, string> { [extra.Item1] = extra.Item2, ["gain"] = "6" })!;
            using var input = new MemoryStream(source);
            using var encoded = new MemoryStream();
            Assert.Null(ConverterRegistry.Find(id)!.Convert(input, encoded, options));

            var back = Volume(encoded.ToArray());

            Assert.InRange(back.Max(Math.Abs), 0.47, 0.53);
        }
    }

    [Theory]
    [InlineData("gain", "13")]
    [InlineData("gain", "+3")]
    [InlineData("compress", "-41")]
    [InlineData("ratio", "3")]
    [InlineData("normalize", "-8")]
    [InlineData("limit", "1")]
    public void Valores_fora_da_regua_sao_recusados(string key, string value)
    {
        // SEC-13: o Worker valida com o mesmo catálogo.
        var info = Core.Conversions.ConverterCatalog.Find("audio.mp3")!;

        Assert.Null(Core.Conversions.ConverterCatalog.ResolveOptions(info, new Dictionary<string, string> { [key] = value }));
    }

    private static float[] Volume(byte[] source, params (string Key, string Value)[] options)
    {
        var info = Core.Conversions.ConverterCatalog.Find("audio.wav")!;
        // Sempre 16 bits: é o que TestAudio.Samples lê (uma origem de 24 bits voltaria em 24 no "Original").
        var given = options.ToDictionary(o => o.Key, o => o.Value);
        given.TryAdd("depth", "16");
        var resolved = Core.Conversions.ConverterCatalog.ResolveOptions(info, given)!;
        using var input = new MemoryStream(source);
        using var output = new MemoryStream();

        Assert.Null(ConverterRegistry.Find("audio.wav")!.Convert(input, output, resolved));
        return TestAudio.Samples(output.ToArray());
    }

    private static double Peak(byte[] wav) => Peak(TestAudio.Samples(wav));

    private static double Peak(float[] samples) => samples.Max(Math.Abs);

    /// <summary>Diferença em dB entre o RMS do trecho forte e o do fraco (segundos pares e ímpares, 48 kHz estéreo).</summary>
    private static double Range(float[] samples)
    {
        const int second = 48000 * 2;
        double loud = 0, quiet = 0;
        long loudCount = 0, quietCount = 0;
        for (var i = 0; i < samples.Length; i++)
        {
            // Ignora os primeiros 200 ms de cada trecho, onde o compressor ainda está reagindo.
            if (i % second < second / 5)
            {
                continue;
            }

            if (i / second % 2 == 0)
            {
                loud += samples[i] * samples[i];
                loudCount++;
            }
            else
            {
                quiet += samples[i] * samples[i];
                quietCount++;
            }
        }

        return 10 * Math.Log10((loud / loudCount) / (quiet / quietCount));
    }
}
