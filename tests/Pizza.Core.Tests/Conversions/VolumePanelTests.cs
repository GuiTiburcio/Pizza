using Pizza.Core.Conversions;

namespace Pizza.Core.Tests.Conversions;

/// <summary>Adendo do ADR-035, pranchas AU-3 e AU-5: linha Volume no painel e a página Volume com os quatro efeitos.</summary>
public class VolumePanelTests
{
    private static OptionsPanelState Painel() => new(ConverterCatalog.Find("audio.mp3")!);

    private static int Row(OptionsPanelState panel, string key) => panel.Rows.ToList().FindIndex(row => row.Key == key);

    private static PanelTarget Choice(OptionsPanelState panel, string key, int choice) =>
        panel.Targets.First(target => target.Kind == PanelTargetKind.Choice && target.Row == Row(panel, key) && target.Choice == choice);

    private static PanelTarget Toggle(OptionsPanelState panel, string key) =>
        panel.Targets.First(target => target.Kind == PanelTargetKind.Toggle && target.Row == Row(panel, key));

    [Fact]
    public void Painel_do_audio_tem_a_linha_Volume_em_Original()
    {
        var panel = Painel();

        Assert.Equal(["Qualidade", "Canais", "Taxa de amostragem", "Volume"], panel.Rows.Select(row => row.Label));
        Assert.Equal(0, panel.Selected(Row(panel, "volume")));
        Assert.Null(panel.RowNote(Row(panel, "volume")));
        Assert.DoesNotContain("volume", panel.Options().Keys);
        Assert.Equal("off", panel.Options()["normalize"]);
    }

    [Fact]
    public void Painel_de_imagem_nao_tem_linha_Volume()
    {
        Assert.DoesNotContain(new OptionsPanelState(ConverterCatalog.Find("image.png")!).Rows, row => row.Key == "volume");
    }

    [Fact]
    public void Ajustar_abre_a_pagina_Volume_com_Normalizar_e_Limitar_ligados()
    {
        var main = Painel();

        Assert.Equal(PanelAction.OpenVolume, main.Click(Choice(main, "volume", 1)));

        var volume = main.VolumePage();
        Assert.Equal(PanelPage.Volume, volume.Page);
        Assert.Equal(["Amplificar", "Comprimir · limiar", "Razão", "Normalizar", "Limitar · teto"], volume.Rows.Select(row => row.Label));
        Assert.False(volume.IsEnabled(Row(volume, "gain")));
        Assert.False(volume.IsEnabled(Row(volume, "compress")));
        Assert.True(volume.IsEnabled(Row(volume, "normalize")));
        Assert.True(volume.IsEnabled(Row(volume, "limit")));
        Assert.Equal("−14 LUFS", volume.ValueText(Row(volume, "normalize")));
        Assert.Equal("−1 dB", volume.ValueText(Row(volume, "limit")));
        Assert.Equal("+3 dB", volume.ValueText(Row(volume, "gain")));
        Assert.False(volume.HasMetadataRow);
    }

    [Fact]
    public void Aplicar_leva_os_efeitos_para_a_conversao_e_mostra_o_resumo()
    {
        var main = Painel();
        main.Click(Choice(main, "volume", 1));
        var volume = main.VolumePage();

        volume.Click(Toggle(volume, "gain"));
        volume.Slide(Row(volume, "gain"), volume.SliderTrack(Row(volume, "gain")).Right);
        Assert.Equal(PanelAction.Convert, volume.Press(PanelKey.Enter));
        main.ApplyVolume(volume);

        Assert.Equal(1, main.Selected(Row(main, "volume")));
        Assert.Equal("Amplificar · Normalizar · Limitar", main.RowNote(Row(main, "volume")));
        Assert.Equal("12", main.Options()["gain"]);
        Assert.Equal("-14", main.Options()["normalize"]);
        Assert.Equal("-1", main.Options()["limit"]);
        Assert.Equal("off", main.Options()["compress"]);
    }

    [Fact]
    public void Voltar_nao_muda_nada()
    {
        var main = Painel();
        main.Click(Choice(main, "volume", 1));
        var volume = main.VolumePage();

        volume.Click(Toggle(volume, "compress"));
        Assert.Equal(PanelAction.Cancel, volume.Press(PanelKey.Escape));
        main.CancelVolume();

        Assert.Equal(0, main.Selected(Row(main, "volume")));
        Assert.Equal("off", main.Options()["compress"]);
        Assert.Equal("off", main.Options()["normalize"]);
    }

    [Fact]
    public void Mexer_na_regua_de_um_efeito_desligado_o_liga()
    {
        var volume = Painel().VolumePage();
        var row = Row(volume, "compress");

        volume.Slide(row, volume.SliderTrack(row).Left);

        Assert.True(volume.IsEnabled(row));
        Assert.Equal("-40", volume.Options()["compress"]);
    }

    [Fact]
    public void Espaco_na_caixinha_em_foco_liga_e_desliga()
    {
        var volume = Painel().VolumePage();

        Assert.Equal((PanelTargetKind.Toggle, Row(volume, "gain")), volume.Focus);
        volume.Press(PanelKey.Space);
        Assert.True(volume.IsEnabled(Row(volume, "gain")));
        volume.Press(PanelKey.Space);
        Assert.False(volume.IsEnabled(Row(volume, "gain")));
    }

    [Fact]
    public void Voltar_para_Original_desliga_todos_os_efeitos()
    {
        var main = Painel();
        main.Click(Choice(main, "volume", 1));
        main.ApplyVolume(main.VolumePage());

        main.Click(Choice(main, "volume", 0));

        Assert.Equal("off", main.Options()["normalize"]);
        Assert.Equal("off", main.Options()["limit"]);
        Assert.Null(main.RowNote(Row(main, "volume")));
    }

    [Fact]
    public void Abrir_de_novo_lembra_o_que_foi_aplicado()
    {
        var main = Painel();
        main.Click(Choice(main, "volume", 1));
        var first = main.VolumePage();
        first.Click(Toggle(first, "normalize"));
        main.ApplyVolume(first);

        var again = main.VolumePage();

        Assert.False(again.IsEnabled(Row(again, "normalize")));
        Assert.True(again.IsEnabled(Row(again, "limit")));
    }
}
