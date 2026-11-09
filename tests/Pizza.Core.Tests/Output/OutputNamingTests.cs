using Pizza.Core.Output;

namespace Pizza.Core.Tests.Output;

public class OutputNamingTests
{
    [Fact]
    public void Primeiro_nome_e_o_do_original_com_a_nova_extensao()
    {
        // FR-26.
        Assert.Equal("fachada.png", OutputNaming.Candidates("C:\\pasta\\fachada.jpg", "png").First());
    }

    [Fact]
    public void Conflitos_viram_numeros_em_sequencia()
    {
        // FR-27.
        Assert.Equal(
            ["a.png", "a (1).png", "a (2).png", "a (3).png"],
            OutputNaming.Candidates("a.jpg", "png").Take(4));
    }

    [Theory]
    [InlineData("nota fiscal ação.jpg", "nota fiscal ação.png")]
    [InlineData("foto.final.v2.jpeg", "foto.final.v2.png")]
    [InlineData("sem-extensao", "sem-extensao.png")]
    public void Nomes_com_acentos_espacos_e_pontos(string source, string expected)
    {
        Assert.Equal(expected, OutputNaming.Candidates(source, "png").First());
    }

    [Fact]
    public void A_lista_e_finita()
    {
        Assert.Equal(OutputNaming.MaxAttempts, OutputNaming.Candidates("a.jpg", "png").Count());
    }

    [Fact]
    public void Nome_temporario_e_aleatorio_oculto_pelo_padrao_da_spec()
    {
        // SEC-30.
        var first = OutputNaming.TemporaryName();

        Assert.Matches("^\\.~pizza-[0-9a-f]{32}\\.tmp$", first);
        Assert.NotEqual(first, OutputNaming.TemporaryName());
    }
}
