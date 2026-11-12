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

    [Fact]
    public void Mesma_extensao_ganha_sufixo_antes_da_numeracao()
    {
        // ADR-033: "escritura - girado.pdf", e com conflito "escritura - girado (1).pdf".
        Assert.Equal(
            ["escritura - girado.pdf", "escritura - girado (1).pdf"],
            OutputNaming.Candidates("C:\\pasta\\escritura.pdf", "pdf", " - girado").Take(2));
    }

    [Theory]
    [InlineData("pages", 1, 5, " - pág 01")]
    [InlineData("pages", 12, 12, " - pág 12")]
    [InlineData("pages", 7, 150, " - pág 007")]
    [InlineData("half", 1, 2, " - parte 1")]
    [InlineData("half", 2, 2, " - parte 2")]
    public void Partes_do_dividir_ficam_em_ordem_no_explorer(string mode, int part, int count, string expected)
    {
        // Prancha 6A-1: "escritura - pág 01.pdf" e "escritura - parte 1.pdf".
        Assert.Equal(expected, OutputNaming.PartSuffix(mode, part, count));
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
