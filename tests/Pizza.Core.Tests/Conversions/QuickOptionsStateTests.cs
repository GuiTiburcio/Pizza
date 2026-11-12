using Pizza.Core.Conversions;

namespace Pizza.Core.Tests.Conversions;

/// <summary>TST-01 / ADR-027: faixa preserva contexto sem executar conversão.</summary>
public class QuickOptionsStateTests
{
    [Theory]
    [InlineData("image.jpg", "quality", "75")]
    [InlineData("image.webp", "quality", "75")]
    [InlineData("image.avif", "quality", "75")]
    [InlineData("image.png", "resolution", "original")]
    [InlineData("image.pdf", "resolution", "original")]
    [InlineData("image.ico", "size", "256")]
    public void Formato_expoe_opcao_principal_e_padrao_do_worker(string id, string key, string value)
    {
        var state = new QuickOptionsState();
        state.PointAt(ConverterCatalog.Find(id));
        Assert.Equal(key, state.Primary!.Key);
        Assert.Equal(value, state.Options()![key]);
    }

    [Fact]
    public void Trajeto_vazio_e_mesma_fatia_preservam_escolha_mas_outra_fatia_restaura_padrao()
    {
        var state = new QuickOptionsState();
        state.PointAt(ConverterCatalog.Find("image.jpg"));
        state.Step(1);
        state.PointAt(null);
        state.PointAt(ConverterCatalog.Find("image.jpg"));
        Assert.Equal("60", state.Options()!["quality"]);
        state.PointAt(ConverterCatalog.Find("image.webp"));
        Assert.Equal("75", state.Options()!["quality"]);
    }

    [Fact]
    public void Extremos_nao_dao_a_volta_e_as_demais_opcoes_ficam_no_padrao()
    {
        var state = new QuickOptionsState();
        state.PointAt(ConverterCatalog.Find("image.png"));
        state.Step(int.MaxValue);
        Assert.Equal("50", state.Options()!["resolution"]);
        Assert.Equal("balanced", state.Options()!["compression"]);
        Assert.Equal("strip", state.Options()![ConverterCatalog.MetadataKey]);
        state.Step(int.MinValue);
        Assert.Equal("original", state.Options()!["resolution"]);
    }

    [Theory]
    [InlineData("image.gif")]
    [InlineData("image.tiff")]
    public void Fatia_sem_opcoes_oculta_a_faixa(string id)
    {
        var state = new QuickOptionsState();
        state.PointAt(ConverterCatalog.Find("image.jpg"));
        state.PointAt(ConverterCatalog.Find(id));
        state.Step(1);
        Assert.Null(state.Primary);
        Assert.Null(state.Options());
    }

    [Theory]
    [InlineData("image.webp", "Qualidade do WEBP", 2, "75% · padrão")]
    [InlineData("image.jpg", "Qualidade do JPG", 0, "95%")]
    [InlineData("image.png", "Resolução do PNG", 0, "padrão")]
    [InlineData("image.pdf", "Resolução do PDF", 1, "150 dpi")]
    [InlineData("image.ico", "Tamanho do ICO", 2, "48 px")]
    [InlineData("pdf.rotate", "Girar páginas", 0, "90° · padrão")]
    [InlineData("pdf.rotate", "Girar páginas", 1, "180°")]
    [InlineData("pdf.rotate", "Girar páginas", 2, "90°")]
    [InlineData("pdf.split", "Como dividir", 0, "1 arquivo por pág. · padrão")]
    [InlineData("pdf.split", "Como dividir", 1, "2 arquivos")]
    public void Faixa_mostra_titulo_e_detalhe_de_cada_nivel(string id, string title, int level, string detail)
    {
        // Prancha 4b-1: "Qualidade do WEBP", com o valor embaixo do nome e o padrão marcado.
        var state = new QuickOptionsState();
        state.PointAt(ConverterCatalog.Find(id));
        Assert.Equal(title, state.Title);
        Assert.Equal(detail, state.Detail(level));
    }

    [Fact]
    public void Detalhe_nao_repete_o_nome_do_nivel()
    {
        var state = new QuickOptionsState();
        state.PointAt(ConverterCatalog.Find("image.png"));
        Assert.Equal("75%", state.Primary!.Choices[1].Label);
        Assert.Null(state.Detail(1));
    }

    [Fact]
    public void Roda_troca_o_nivel_so_a_cada_dente_inteiro()
    {
        // Touchpads mandam frações de 120; para baixo avança, como na troca de aba.
        var state = new QuickOptionsState();
        state.PointAt(ConverterCatalog.Find("image.jpg"));
        Assert.False(state.Wheel(-60));
        Assert.True(state.Wheel(-60));
        Assert.Equal("60", state.Options()!["quality"]);
        Assert.True(state.Wheel(240));
        Assert.Equal("85", state.Options()!["quality"]);
    }

    [Fact]
    public void Roda_no_extremo_nao_muda_nada()
    {
        var state = new QuickOptionsState();
        state.PointAt(ConverterCatalog.Find("image.jpg"));
        state.Step(int.MinValue);
        Assert.False(state.Wheel(120));
        Assert.False(new QuickOptionsState().Wheel(-120));
    }

    [Fact]
    public void Troca_de_pagina_ou_fim_do_menu_limpa_contexto()
    {
        var state = new QuickOptionsState();
        state.PointAt(ConverterCatalog.Find("image.avif"));
        state.Step(1);
        state.Clear();
        state.Step(1);
        Assert.Null(state.Converter);
        Assert.Null(state.Options());
        state.PointAt(ConverterCatalog.Find("image.avif"));
        Assert.Equal("75", state.Options()!["quality"]);
    }
}
