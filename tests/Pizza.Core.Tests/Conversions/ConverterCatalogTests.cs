using Pizza.Core.Conversions;

namespace Pizza.Core.Tests.Conversions;

/// <summary>SPECS §10.3: <c>converterId</c> na lista do registro e <c>options</c> validadas pelo esquema.</summary>
public class ConverterCatalogTests
{
    private static ConverterInfo Png => ConverterCatalog.Find("image.png")!;

    private static Dictionary<string, string> Given(params (string Key, string Value)[] options) =>
        options.ToDictionary(o => o.Key, o => o.Value);

    [Fact]
    public void Conversor_fora_do_catalogo_nao_existe()
    {
        Assert.Null(ConverterCatalog.Find("image.bmp"));
    }

    [Fact]
    public void Ids_nao_se_repetem()
    {
        Assert.Equal(ConverterCatalog.All.Count, ConverterCatalog.All.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void Padrao_de_cada_opcao_esta_entre_as_escolhas()
    {
        foreach (var option in ConverterCatalog.All.SelectMany(c => c.Options))
        {
            Assert.Contains(option.Choices, choice => choice.Value == option.Default);
        }
    }

    [Fact]
    public void Sem_opcoes_usa_o_padrao()
    {
        var resolved = ConverterCatalog.ResolveOptions(Png, Given());

        Assert.Equal("original", resolved!["resolution"]);
        Assert.Equal("balanced", resolved["compression"]);
    }

    [Fact]
    public void Opcao_valida_e_mantida_e_as_demais_ficam_no_padrao()
    {
        var resolved = ConverterCatalog.ResolveOptions(Png, Given(("compression", "fast")));

        Assert.Equal("fast", resolved!["compression"]);
        Assert.Equal("original", resolved["resolution"]);
    }

    [Theory]
    [InlineData("20")]
    [InlineData("67")]
    [InlineData("99")]
    public void Regua_aceita_qualquer_porcentagem_inteira_de_20_a_99(string value)
    {
        // ADR-030: o 100% é "original"; o Worker valida o intervalo com o mesmo catálogo (SEC-13).
        Assert.Equal(value, ConverterCatalog.ResolveOptions(Png, Given(("resolution", value)))!["resolution"]);
    }

    [Theory]
    [InlineData("compression", "ultra")]
    [InlineData("resolution", "200")]
    [InlineData("resolution", "100")]
    [InlineData("resolution", "19")]
    [InlineData("resolution", "067")]
    [InlineData("resolution", "+67")]
    [InlineData("resolution", " 67")]
    [InlineData("resolution", "6.7")]
    [InlineData("resolution", "")]
    [InlineData("caminho", "C:\\a.png")]
    public void Opcao_fora_do_esquema_e_recusada(string key, string value)
    {
        Assert.Null(ConverterCatalog.ResolveOptions(Png, Given((key, value))));
    }
}
