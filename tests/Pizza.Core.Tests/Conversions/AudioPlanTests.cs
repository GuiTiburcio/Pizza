using Pizza.Core.Conversions;
using Pizza.Core.Ipc;

namespace Pizza.Core.Tests.Conversions;

/// <summary>Adendo do ADR-035: tamanho previsto no painel, a partir do "info" do Worker e das opções.</summary>
public class AudioPlanTests
{
    // 3 min 12 s de CD: 44,1 kHz, estéreo, 16 bits.
    private static readonly MediaInfo Cd = new(192_000, 44100, 2, 16, LosslessPercent: 0);

    private static IReadOnlyDictionary<string, string> Options(string id, params (string Key, string Value)[] given) =>
        ConverterCatalog.ResolveOptions(ConverterCatalog.Find(id)!, given.ToDictionary(o => o.Key, o => o.Value))!;

    [Fact]
    public void Mp3_e_taxa_vezes_duracao()
    {
        var size = AudioPlan.Estimate("audio.mp3", Options("audio.mp3"), Cd);

        Assert.Equal(192_000L * 192 / 8, size.Bytes);
        Assert.False(size.Exact);
        Assert.Equal("≈ 4,4 MB", size.Text);
    }

    [Fact]
    public void Wav_e_exato()
    {
        var size = AudioPlan.Estimate("audio.wav", Options("audio.wav"), Cd);

        Assert.Equal(44 + (44100L * 2 * 2 * 192), size.Bytes);
        Assert.True(size.Exact);
        Assert.Equal("32,3 MB", size.Text);
    }

    [Fact]
    public void Sem_perda_e_estimado_em_55_por_cento_do_wav()
    {
        var wav = 44100L * 2 * 2 * 192;

        Assert.Equal(wav * 55 / 100, AudioPlan.Estimate("audio.flac", Options("audio.flac"), Cd).Bytes);
        Assert.Equal(wav * 55 / 100, AudioPlan.Estimate("audio.m4a", Options("audio.m4a", ("bitrate", "lossless")), Cd).Bytes);
    }

    [Fact]
    public void Sem_perda_usa_a_compressao_medida_pelo_probe()
    {
        var wav = 44100L * 2 * 2 * 192;

        Assert.Equal(wav * 18 / 100, AudioPlan.Estimate("audio.flac", Options("audio.flac"), Cd with { LosslessPercent = 18 }).Bytes);
    }

    [Fact]
    public void Origem_com_perda_sai_em_16_bits_no_original()
    {
        // MP3 e AAC não informam bits (0): 24 só dobraria o arquivo sem ganho de qualidade.
        Assert.Equal(16, AudioPlan.Target("wav", Options("audio.wav"), 48000, 2, 0).Bits);
        Assert.Equal(24, AudioPlan.Target("wav", Options("audio.wav"), 48000, 2, 24).Bits);
    }

    [Fact]
    public void Opcoes_mudam_o_tamanho()
    {
        var stereo = AudioPlan.Estimate("audio.wav", Options("audio.wav"), Cd).Bytes;
        var mono = AudioPlan.Estimate("audio.wav", Options("audio.wav", ("channels", "mono")), Cd).Bytes;
        var deep = AudioPlan.Estimate("audio.wav", Options("audio.wav", ("depth", "24")), Cd).Bytes;
        var fast = AudioPlan.Estimate("audio.mp3", Options("audio.mp3", ("bitrate", "320")), Cd).Bytes;

        Assert.Equal(44 + ((stereo - 44) / 2), mono);
        Assert.Equal(44 + ((stereo - 44) * 3 / 2), deep);
        Assert.Equal(320_000L * 192 / 8, fast);
    }

    [Theory]
    [InlineData(96000, 48000)]
    [InlineData(22050, 48000)]
    [InlineData(44100, 44100)]
    public void Mp3_so_sai_em_44_1_ou_48_kHz(int source, int expected)
    {
        // A mesma regra do Worker: em "Original", outra taxa vira 48 kHz.
        var (rate, _, _) = AudioPlan.Target("mp3", Options("audio.mp3"), source, 2, 16);

        Assert.Equal(expected, rate);
    }

    [Fact]
    public void Arquivo_pequeno_aparece_em_KB()
    {
        var size = AudioPlan.Estimate("audio.mp3", Options("audio.mp3", ("bitrate", "128")), Cd with { DurationMs = 30_000 });

        Assert.Equal("≈ 469 KB", size.Text);
    }

    [Fact]
    public void Painel_mostra_traco_ate_o_info_chegar_e_depois_o_tamanho()
    {
        var panel = new OptionsPanelState(ConverterCatalog.Find("audio.mp3")!);

        Assert.True(panel.HasSizeRow);
        Assert.Equal("—", panel.SizeText);
        panel.SetInfo(Cd);
        Assert.Equal("≈ 4,4 MB", panel.SizeText);
        panel.Press(PanelKey.End);
        Assert.Equal("≈ 7,3 MB", panel.SizeText);
    }

    [Fact]
    public void Imagem_nao_tem_linha_de_tamanho()
    {
        Assert.False(new OptionsPanelState(ConverterCatalog.Find("image.png")!).HasSizeRow);
    }
}
