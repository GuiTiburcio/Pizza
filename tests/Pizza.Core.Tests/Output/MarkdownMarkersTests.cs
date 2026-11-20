using System.Text;
using Pizza.Core.Output;

namespace Pizza.Core.Tests.Output;

/// <summary>
/// ADR-038: o Worker escreve U+E000, o número da imagem e U+E000 onde vai o nome dela; o residente troca só
/// isso pelo nome final que ele escolheu. Qualquer marcador fora do formato recusa o arquivo.
/// </summary>
public class MarkdownMarkersTests
{
    private const char Marker = '\uE000';

    private static string Replace(string markdown, string[] names, int buffer = 4096)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
        using var output = new MemoryStream();
        MarkdownMarkers.Replace(input, output, names, buffer);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    [Fact]
    public void Marcadores_viram_os_nomes_do_residente()
    {
        var markdown = $"# Relatório\n\n![[{Marker}1{Marker}]]\n\ntexto ![[{Marker}2{Marker}]] e de novo ![[{Marker}1{Marker}]]\n";

        Assert.Equal(
            "# Relatório\n\n![[relatorio - imagem 1.png]]\n\ntexto ![[relatorio - imagem 2 (1).jpg]] e de novo ![[relatorio - imagem 1.png]]\n",
            Replace(markdown, ["relatorio - imagem 1.png", "relatorio - imagem 2 (1).jpg"]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void Marcador_partido_entre_leituras_tambem_e_trocado(int buffer)
    {
        Assert.Equal("aé ![[x.png]] fim", Replace($"aé ![[{Marker}1{Marker}]] fim", ["x.png"], buffer));
    }

    [Fact]
    public void Outros_caracteres_de_uso_privado_passam_iguais()
    {
        // U+E001 e U+F000 começam com o mesmo byte do marcador, mas não são marcadores.
        Assert.Equal("\uE001\uF000", Replace("\uE001\uF000", []));
    }

    [Theory]
    [InlineData("\uE0001")] // sem o fim
    [InlineData("\uE000\uE000")] // sem número
    [InlineData("\uE000a\uE000")] // não é número
    [InlineData("\uE0000\uE000")] // imagem 0
    [InlineData("\uE0003\uE000")] // além das imagens
    [InlineData("\uE00001\uE000")] // zero à esquerda
    [InlineData("\uE0001234567\uE000")] // número longo demais
    [InlineData("\uE0001x")] // fim errado
    public void Marcador_fora_do_formato_recusa_o_arquivo(string markdown)
    {
        Assert.Throws<InvalidDataException>(() => Replace(markdown, ["a.png", "b.png"]));
    }

    [Theory]
    [InlineData("relatorio", "relatorio")]
    [InlineData("ata [final] #2 | v^3", "ata _final_ _2 _ v_3")]
    public void Nome_das_imagens_tira_o_que_o_obsidian_nao_aceita_em_links(string stem, string expected)
    {
        Assert.Equal(expected + ".docx", OutputNaming.LinkSafe($"C:\\pasta\\{stem}.docx"));
    }
}
