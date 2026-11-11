using Pizza.Core.Conversions;

namespace Pizza.Core.Tests.Conversions;

/// <summary>FR-20, FR-21, FR-23 e NFR-06: painel de opções, inclusive só pelo teclado.</summary>
public class OptionsPanelStateTests
{
    private static OptionsPanelState Panel(string id) => new(ConverterCatalog.Find(id)!);

    [Fact]
    public void Enter_converte_com_os_padroes_sem_nenhuma_outra_escolha()
    {
        // FR-20.
        var panel = Panel("image.png");

        Assert.Equal(PanelAction.Convert, panel.Press(PanelKey.Enter));
        Assert.Equal("original", panel.Options()["resolution"]);
        Assert.Equal("balanced", panel.Options()["compression"]);
        Assert.Equal("strip", panel.Options()[ConverterCatalog.MetadataKey]);
    }

    [Fact]
    public void Esc_cancela()
    {
        Assert.Equal(PanelAction.Cancel, Panel("image.png").Press(PanelKey.Escape));
    }

    [Theory]
    [InlineData("image.tiff")]
    [InlineData("image.gif")]
    public void Conversao_sem_opcoes_nao_abre_painel(string id)
    {
        // FR-21.
        Assert.False(ConverterCatalog.Find(id)!.HasPanel);
    }

    [Theory]
    [InlineData("image.jpg")]
    [InlineData("image.png")]
    [InlineData("image.webp")]
    public void Manter_metadados_comeca_desmarcado_onde_o_formato_guarda(string id)
    {
        // FR-23.
        var panel = Panel(id);

        Assert.True(panel.CanKeepMetadata);
        Assert.False(panel.KeepMetadata);
    }

    [Theory]
    [InlineData("image.pdf")]
    [InlineData("image.ico")]
    public void Formato_sem_metadados_mostra_a_caixa_desativada(string id)
    {
        var panel = Panel(id);

        Assert.False(panel.CanKeepMetadata);
        Assert.DoesNotContain(panel.Targets, target => target.Kind == PanelTargetKind.Keep);
        Assert.DoesNotContain(ConverterCatalog.MetadataKey, panel.Options().Keys);
    }

    [Fact]
    public void Setas_trocam_a_escolha_sem_dar_a_volta()
    {
        var panel = Panel("image.png");

        panel.Press(PanelKey.Right);
        Assert.Equal("75", panel.Options()["resolution"]);
        panel.Press(PanelKey.Right);
        panel.Press(PanelKey.Right);
        Assert.Equal("50", panel.Options()["resolution"]);
        panel.Press(PanelKey.Left);
        panel.Press(PanelKey.Left);
        panel.Press(PanelKey.Left);
        Assert.Equal("original", panel.Options()["resolution"]);
    }

    [Fact]
    public void Tab_percorre_tudo_e_volta_ao_inicio()
    {
        var panel = Panel("image.png");

        Assert.Equal((PanelTargetKind.Choice, 0), panel.Focus);
        panel.Press(PanelKey.Tab);
        Assert.Equal((PanelTargetKind.Choice, 1), panel.Focus);
        panel.Press(PanelKey.Tab);
        Assert.Equal((PanelTargetKind.Keep, 0), panel.Focus);
        panel.Press(PanelKey.Tab);
        Assert.Equal((PanelTargetKind.Cancel, 0), panel.Focus);
        panel.Press(PanelKey.Tab);
        Assert.Equal((PanelTargetKind.Convert, 0), panel.Focus);
        panel.Press(PanelKey.Tab);
        Assert.Equal((PanelTargetKind.Choice, 0), panel.Focus);
    }

    [Fact]
    public void Shift_tab_volta_e_a_caixa_desativada_nao_recebe_foco()
    {
        var panel = Panel("image.pdf");

        panel.Press(PanelKey.ShiftTab);
        Assert.Equal((PanelTargetKind.Convert, 0), panel.Focus);
        panel.Press(PanelKey.ShiftTab);
        Assert.Equal((PanelTargetKind.Cancel, 0), panel.Focus);
        panel.Press(PanelKey.ShiftTab);
        Assert.Equal((PanelTargetKind.Choice, 0), panel.Focus);
    }

    [Fact]
    public void Espaco_marca_e_desmarca_manter_metadados()
    {
        var panel = Panel("image.jpg");
        panel.Press(PanelKey.Tab);

        Assert.Equal(PanelAction.None, panel.Press(PanelKey.Space));
        Assert.Equal("keep", panel.Options()[ConverterCatalog.MetadataKey]);
        panel.Press(PanelKey.Space);
        Assert.Equal("strip", panel.Options()[ConverterCatalog.MetadataKey]);
    }

    [Fact]
    public void Enter_sobre_cancelar_cancela()
    {
        var panel = Panel("image.pdf");
        panel.Press(PanelKey.ShiftTab);
        panel.Press(PanelKey.ShiftTab);

        Assert.Equal(PanelAction.Cancel, panel.Press(PanelKey.Enter));
    }

    [Fact]
    public void Espaco_sobre_converter_converte()
    {
        var panel = Panel("image.pdf");
        panel.Press(PanelKey.ShiftTab);

        Assert.Equal(PanelAction.Convert, panel.Press(PanelKey.Space));
    }

    [Fact]
    public void Clique_escolhe_e_move_o_foco()
    {
        var panel = Panel("image.png");
        var target = panel.Targets.Single(t => t is { Kind: PanelTargetKind.Choice, Row: 1, Choice: 0 });

        Assert.Equal(PanelAction.None, panel.Click(target));

        Assert.Equal("fast", panel.Options()["compression"]);
        Assert.Equal((PanelTargetKind.Choice, 1), panel.Focus);
    }

    [Fact]
    public void Clique_nos_botoes_devolve_a_acao()
    {
        var panel = Panel("image.png");

        Assert.Equal(PanelAction.Cancel, panel.Click(panel.Targets.Single(t => t.Kind == PanelTargetKind.Cancel)));
        Assert.Equal(PanelAction.Convert, panel.Click(panel.Targets.Single(t => t.Kind == PanelTargetKind.Convert)));
    }

    [Fact]
    public void Opcoes_do_painel_sempre_passam_na_validacao_do_worker()
    {
        foreach (var converter in ConverterCatalog.All.Where(c => c.HasPanel))
        {
            var panel = new OptionsPanelState(converter);
            foreach (var target in panel.Targets.Where(t => t.Kind is PanelTargetKind.Choice or PanelTargetKind.Keep))
            {
                panel.Click(target);
                Assert.NotNull(ConverterCatalog.ResolveOptions(converter, panel.Options()));
            }
        }
    }

    [Fact]
    public void Alvos_ficam_dentro_do_painel_e_nao_se_sobrepoem()
    {
        foreach (var converter in ConverterCatalog.All.Where(c => c.HasPanel))
        {
            var panel = new OptionsPanelState(converter);
            var targets = panel.Targets;
            foreach (var target in targets)
            {
                Assert.True(target.Bounds is { Left: >= 0, Top: >= 0 });
                Assert.True(target.Bounds.Right <= panel.Width && target.Bounds.Bottom <= panel.Height);
            }

            for (var i = 0; i < targets.Count; i++)
            {
                for (var j = i + 1; j < targets.Count; j++)
                {
                    Assert.False(targets[i].Bounds.Overlaps(targets[j].Bounds), $"{converter.Id}: {i} e {j}");
                }
            }
        }
    }

    [Fact]
    public void Cada_controle_tem_alvo_de_pelo_menos_44_pixels_e_responde_na_borda()
    {
        // Fase 4b / NFR-06: o alvo inteiro é clicável, inclusive fora do texto.
        foreach (var converter in ConverterCatalog.All.Where(c => c.HasPanel))
        {
            var panel = new OptionsPanelState(converter);
            foreach (var target in panel.Targets)
            {
                Assert.True(target.Bounds.Right - target.Bounds.Left >= 44, converter.Id);
                Assert.True(target.Bounds.Bottom - target.Bounds.Top >= 44, converter.Id);
                Assert.Equal(target, panel.TargetAt(target.Bounds.Left + 1, target.Bounds.Top + 1));
                Assert.Equal(target, panel.TargetAt(target.Bounds.Right - 1, target.Bounds.Bottom - 1));
            }
        }
    }

    [Fact]
    public void Ponto_encontra_o_alvo_certo()
    {
        var panel = Panel("image.png");
        var convert = panel.Targets.Single(t => t.Kind == PanelTargetKind.Convert);

        Assert.Equal(convert, panel.TargetAt((convert.Bounds.Left + convert.Bounds.Right) / 2, convert.Bounds.Top + 1));
        Assert.Null(panel.TargetAt(1, 1));
    }

    [Fact]
    public void Bolha_parada_cobre_a_opcao_escolhida()
    {
        // ADR-027: a opção escolhida em cada grupo é marcada pela bolha de vidro.
        var state = new OptionsPanelState(ConverterCatalog.Find("image.png")!);
        var choice = state.Targets.First(target => target.Kind == PanelTargetKind.Choice && target.Row == 1 && target.Choice == state.Selected(1)).Bounds;

        var bubble = state.BubbleAt(1, state.Selected(1));

        Assert.Equal((choice.Left, choice.Right, choice.Top, choice.Bottom), (bubble.Left, bubble.Right, bubble.Top, bubble.Bottom));
    }

    [Fact]
    public void Bolha_no_meio_fica_entre_as_duas_opcoes_e_nao_passa_das_pontas()
    {
        var state = new OptionsPanelState(ConverterCatalog.Find("image.jpg")!);
        var first = state.BubbleAt(0, 0);
        var second = state.BubbleAt(0, 1);

        Assert.Equal((first.Left + second.Left) / 2, state.BubbleAt(0, 0.5).Left, 3);
        Assert.Equal(first.Left, state.BubbleAt(0, -1).Left, 3);
        Assert.Equal(state.BubbleAt(0, 3).Left, state.BubbleAt(0, 9).Left, 3);
    }

    [Fact]
    public void Setas_mudam_a_opcao_que_a_bolha_segue()
    {
        var state = new OptionsPanelState(ConverterCatalog.Find("image.jpg")!);
        var before = state.Selected(0);

        state.Press(PanelKey.Right);

        Assert.Equal(before + 1, state.Selected(0));
    }
}