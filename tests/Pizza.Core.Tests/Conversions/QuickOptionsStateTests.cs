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
