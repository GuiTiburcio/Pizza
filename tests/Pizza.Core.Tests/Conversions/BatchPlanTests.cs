using Pizza.Core.Conversions;
using Pizza.Core.Tests.Gesture;

namespace Pizza.Core.Tests.Conversions;

/// <summary>FR-31 / TST-01: planejamento contabiliza todo o lote sem executar conversões.</summary>
[Collection(DocumentEngineSwitch.Name)]
public class BatchPlanTests
{
    [Fact]
    public void Lote_misto_preserva_todos_os_arquivos_e_so_oferece_saidas_validas_por_grupo()
    {
        using var _ = DocumentEngineSwitch.Set(false);
        string[] paths = ["a.jpg", "b.png", "c.mp3", "d.pdf", "e.docx", "f.xlsx", "g.JPG"];
        var plan = new BatchPlan(paths);

        Assert.Equal(paths, plan.Paths);
        Assert.Equal(6, plan.Groups.Count);
        Assert.Empty(plan.Unsupported);
        Assert.False(plan.IsReady);
        Assert.Equal(paths.Order(), plan.Groups.SelectMany(group => group.Paths).Order());
        Assert.Equal(["a.jpg", "g.JPG"], plan.Groups[0].Paths);
        Assert.All(plan.Groups, group => Assert.All(group.Converters, converter =>
        {
            Assert.False(converter.Merges);
            Assert.All(group.Paths, path => Assert.True(converter.Accepts([path])));
        }));
        Assert.DoesNotContain(plan.Groups.SelectMany(group => group.Converters), converter => converter.DocumentEngine);
    }

    [Fact]
    public void Arquivo_sem_suporte_no_inicio_nao_esconde_os_demais_e_bloqueia_a_confirmacao()
    {
        using var _ = DocumentEngineSwitch.Set(false);
        var plan = new BatchPlan(["sem-suporte.exe", "foto.jpg", "voz.mp3"]);

        Assert.Equal(["sem-suporte.exe"], plan.Unsupported);
        Assert.Equal(2, plan.Groups.Count);
        ChooseAll(plan);
        Assert.False(plan.IsReady);
    }

    [Fact]
    public void Revisao_so_fica_pronta_depois_de_escolher_todos_os_grupos_e_volta_invalida_o_aceite()
    {
        var plan = new BatchPlan(["foto.jpg", "voz.mp3"]);
        ChooseAll(plan);
        Assert.True(plan.IsReady);
        plan.ClearChoice(0);
        Assert.False(plan.IsReady);
    }

    [Fact]
    public void Escolhas_invalidas_nao_alteram_o_plano()
    {
        var plan = new BatchPlan(["foto.jpg"]);
        Assert.False(plan.TryChoose(0, "audio.mp3", new Dictionary<string, string>()));
        Assert.False(plan.TryChoose(0, "image.png", new Dictionary<string, string> { ["compression"] = "invalida" }));
        Assert.False(plan.TryChoose(-1, "image.png", new Dictionary<string, string>()));
        Assert.False(plan.TryChoose(1, "image.png", new Dictionary<string, string>()));
        Assert.False(plan.IsReady);
        Assert.True(plan.TryChoose(0, "image.png", new Dictionary<string, string>()));
        var previous = plan.Groups[0].Choice;
        Assert.False(plan.TryChoose(0, "audio.mp3", new Dictionary<string, string>()));
        Assert.Same(previous, plan.Groups[0].Choice);
    }

    [Fact]
    public void Escolha_copia_opcoes_e_resolve_padroes_sem_depender_da_mutacao_do_chamador()
    {
        var paths = new[] { "foto.jpg" };
        var options = new Dictionary<string, string> { ["compression"] = "maximum" };
        var plan = new BatchPlan(paths);
        Assert.True(plan.TryChoose(0, "image.png", options));
        paths[0] = "outra.exe";
        options["compression"] = "invalida";

        Assert.Equal("foto.jpg", plan.Paths[0]);
        Assert.Equal("maximum", plan.Groups[0].Choice!.Options["compression"]);
        Assert.Equal("original", plan.Groups[0].Choice!.Options["resolution"]);
    }

    [Fact]
    public void Documentos_da_mesma_aba_com_saidas_distintas_ficam_separados_no_Jumbo()
    {
        using var _ = DocumentEngineSwitch.Set(true);
        var plan = new BatchPlan(["texto.docx", "tabela.xlsx", "slides.pptx"]);

        Assert.Equal(3, plan.Groups.Count);
        Assert.All(plan.Groups, group => Assert.Equal(ConverterCatalog.DocsTab, group.Tab));
        Assert.All(plan.Groups, group => Assert.Contains(group.Converters, converter => converter.Id == "document.pdf"));
        Assert.DoesNotContain(plan.Groups[1].Converters, converter => converter.Id == "document.txt");
    }

    [Fact]
    public void Quantidade_e_duplicacao_sao_validadas_sem_descartar_entradas()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BatchPlan([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BatchPlan(Enumerable.Range(0, 501).Select(i => $"{i}.jpg").ToArray()));
        Assert.Throws<ArgumentException>(() => new BatchPlan(["a.jpg", "A.JPG"]));
        Assert.Throws<ArgumentException>(() => new BatchPlan([" "]));
        Assert.Equal(500, new BatchPlan(Enumerable.Range(0, 500).Select(i => $"{i}.jpg").ToArray()).Paths.Count);
    }

    private static void ChooseAll(BatchPlan plan)
    {
        for (var group = 0; group < plan.Groups.Count; group++)
        {
            Assert.True(plan.TryChoose(group, plan.Groups[group].Converters[0].Id, new Dictionary<string, string>()));
        }
    }
}
