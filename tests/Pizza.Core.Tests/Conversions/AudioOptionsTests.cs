using Pizza.Core.Conversions;

namespace Pizza.Core.Tests.Conversions;

/// <summary>ADR-035, pranchas AU-1 a AU-4: régua de qualidade em degraus, faixa com três atalhos e sem "Manter metadados".</summary>
public class AudioOptionsTests
{
    private static OptionsPanelState Panel(string id) => new(ConverterCatalog.Find(id)!);

    [Fact]
    public void Qualidade_do_MP3_e_uma_regua_em_degraus_que_comeca_no_padrao()
    {
        var panel = Panel("audio.mp3");

        Assert.True(panel.IsSlider(0));
        Assert.Equal("192", panel.ValueText(0));
        Assert.Equal("kbps", panel.ValueUnit(0));
        Assert.Equal(4 / 7.0, panel.SliderAmount(0), 3);
    }

    [Fact]
    public void Setas_e_Home_End_andam_um_degrau_e_vao_as_pontas()
    {
        var panel = Panel("audio.mp3");

        panel.Press(PanelKey.Right);
        Assert.Equal("224", panel.Options()["bitrate"]);
        panel.Press(PanelKey.ShiftLeft);
        Assert.Equal("192", panel.Options()["bitrate"]);
        panel.Press(PanelKey.End);
        Assert.Equal("320", panel.Options()["bitrate"]);
        panel.Press(PanelKey.Home);
        Assert.Equal("96", panel.Options()["bitrate"]);
    }

    [Fact]
    public void Arrastar_para_no_degrau_mais_proximo()
    {
        var panel = Panel("audio.mp3");
        var track = panel.SliderTrack(0);

        panel.Slide(0, track.Left);
        Assert.Equal("96", panel.Options()["bitrate"]);
        panel.Slide(0, track.Left + ((track.Right - track.Left) * 3 / 7) + 2);
        Assert.Equal("160", panel.Options()["bitrate"]);
        panel.Slide(0, track.Right + 50);
        Assert.Equal("320", panel.Options()["bitrate"]);
    }

    [Fact]
    public void Valor_digitado_vai_ao_degrau_mais_proximo()
    {
        var panel = Panel("audio.mp3");
        panel.EditValue(0);

        panel.Type('1');
        panel.Type('3');
        panel.Type('0');
        panel.Press(PanelKey.Tab);

        Assert.Equal("128", panel.Options()["bitrate"]);
    }

    [Fact]
    public void No_fim_da_regua_do_M4A_fica_Sem_perda()
    {
        var panel = Panel("audio.m4a");

        Assert.Equal("160", panel.ValueText(0));
        panel.Press(PanelKey.End);

        Assert.Equal("lossless", panel.Options()["bitrate"]);
        Assert.Equal("Sem perda", panel.ValueText(0));
        Assert.Equal("", panel.ValueUnit(0));
    }

    [Theory]
    [InlineData("audio.mp3")]
    [InlineData("audio.flac")]
    public void Audio_nao_tem_a_linha_de_metadados(string id)
    {
        // ADR-035: a saída sai sem etiquetas e a caixa não aparece.
        var panel = Panel(id);

        Assert.False(panel.HasMetadataRow);
        Assert.DoesNotContain(panel.Targets, target => target.Kind == PanelTargetKind.Keep);
        Assert.DoesNotContain(ConverterCatalog.MetadataKey, panel.Options().Keys);
    }

    [Fact]
    public void Faixa_do_MP3_mostra_tres_atalhos_com_os_kbps()
    {
        var strip = new QuickOptionsState();
        strip.PointAt(ConverterCatalog.Find("audio.mp3"));

        Assert.Equal("Qualidade do MP3", strip.Title);
        Assert.Equal(["Alta", "Equilibrada", "Compacta"], strip.Levels.Select(level => level.Label));
        Assert.Equal(1, strip.Selected);
        Assert.Equal("320 kbps", strip.Detail(0));
        Assert.Equal("192 kbps · padrão", strip.Detail(1));

        strip.Step(-1);
        Assert.Equal("320", strip.Options()!["bitrate"]);
    }

    [Fact]
    public void Faixa_do_FLAC_mostra_a_profundidade()
    {
        var strip = new QuickOptionsState();
        strip.PointAt(ConverterCatalog.Find("audio.flac"));

        Assert.Equal("Profundidade do FLAC", strip.Title);
        Assert.Equal(["Original", "16 bits", "24 bits"], strip.Levels.Select(level => level.Label));
        Assert.Equal("padrão", strip.Detail(0));
        Assert.Equal("CD", strip.Detail(1));
        Assert.Equal("estúdio", strip.Detail(2));
    }
}
