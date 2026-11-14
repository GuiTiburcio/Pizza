using System.Text;
using NetVips;
using Pizza.Converters.Docs;
using Pizza.Core.Output;
using static Pizza.Converters.Tests.TestOffice;

namespace Pizza.Converters.Tests;

/// <summary>
/// DOCX → Markdown no jeito do Obsidian (ADR-034 e ADR-038): o texto num .md e as imagens ao lado. O Worker
/// escreve um marcador (U+E000, número, U+E000) onde vai o nome de cada imagem; quem dá o nome é o residente.
/// </summary>
public sealed class DocxMarkdownConverterTests : IDisposable
{
    private const char M = '\uE000';
    private readonly string _file = Path.Combine(Path.GetTempPath(), "pizza-md-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => File.Delete(_file);

    private (string? Error, string Markdown, List<(PartFormat Format, byte[] Data)> Images) Convert(byte[] docx, IConverter? converter = null)
    {
        using (var source = new MemoryStream(docx))
        using (var destination = File.Create(_file))
        {
            var error = (converter ?? new DocxMarkdownConverter()).Convert(source, destination, new Dictionary<string, string>());
            if (error is not null)
            {
                return (error, "", []);
            }
        }

        using var handle = File.OpenHandle(_file);
        var bytes = File.ReadAllBytes(_file);
        var parts = OutputBundle.ReadFormatted(handle, 2000, "md");
        var data = parts.Select(part => bytes[(int)part.Offset..(int)(part.Offset + part.Length)]).ToList();
        return (null, new UTF8Encoding(false).GetString(data[0]), parts.Skip(1).Select((part, index) => (part.Format, data[index + 1])).ToList());
    }

    private string Markdown(byte[] docx) => Convert(docx).Markdown;

    [Fact]
    public void Arquivo_ouro_no_markdown_do_obsidian()
    {
        var (error, markdown, images) = Convert(DocxHtmlConverterTests.Golden());

        Assert.Null(error);
        Assert.Equal(
            "# Relatório & \"Plano\"\n\n"
            + "# Introdução\n\n"
            + "Texto **negrito** e *itálico* ~~tachado~~ <u>sublinhado</u> ==realce== H<sub>2</sub>O e x<sup>2</sup> **forte** \\<script\\>\n\n"
            + "## Links\n\n"
            + "[site](https://exemplo.com/a?b=1&c=2) perigo interno[^1]\n\n"
            + "- um\n\t1. um.a\n\t2. um.b\n- dois\n\n"
            + "> Citação\n\n"
            + "```\nlinha 1\n  linha 2\n```\n\n"
            + "---\n\n"
            + "| A |  | B |\n| --- | --- | --- |\n| C | D |  |\n\n"
            + $"![[{M}1{M}]]\n\n"
            + "\\[imagem em EMF: salve como PNG no editor e converta de novo\\]\n\n"
            + "[^1]: Fonte *X*.\n",
            markdown);
        var image = Assert.Single(images);
        Assert.Equal(PartFormat.Png, image.Format);
        Assert.Equal(System.Convert.FromBase64String(DocxHtmlConverterTests.Png), image.Data);
    }

    [Fact]
    public void Imagem_repetida_vira_uma_parte_so_e_cada_formato_vai_com_o_seu()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2];
        var docx = RichDocx(
            "<w:p>" + Drawing("rIdA") + Drawing("rIdB") + Drawing("rIdA") + "</w:p>",
            relations: [("rIdA", "image", "media/a.png", false), ("rIdB", "image", "media/b.jpeg", false)],
            media: [("word/media/a.png", System.Convert.FromBase64String(DocxHtmlConverterTests.Png)), ("word/media/b.jpeg", jpeg)]);

        var (error, markdown, images) = Convert(docx);

        Assert.Null(error);
        Assert.Equal($"![[{M}1{M}]]![[{M}2{M}]]![[{M}1{M}]]\n", markdown);
        Assert.Equal([PartFormat.Png, PartFormat.Jpeg], images.Select(image => image.Format));
        Assert.Equal(jpeg, images[1].Data);
    }

    [Fact]
    public void Tiff_vira_png_ao_lado()
    {
        var docx = RichDocx(
            "<w:p>" + Drawing("rIdI") + "</w:p>",
            relations: [("rIdI", "image", "media/image1.tif", false)],
            media: ("word/media/image1.tif", Image.Black(3, 2).TiffsaveBuffer()));

        var (error, _, images) = Convert(docx);

        Assert.Null(error);
        Assert.Equal(PartFormat.Png, Assert.Single(images).Format);
    }

    [Fact]
    public void Texto_do_documento_nao_forja_marcador()
    {
        // ADR-038: só o Worker escreve U+E000; o do documento vira o caractere de substituição.
        Assert.Equal("a\uFFFD1\uFFFDb\n", Markdown(RichDocx(Paragraph($"a{M}1{M}b"))));
    }

    [Theory]
    [InlineData("50% e 100%", "50% e 100%")]
    [InlineData("%%comentário%%", "%\\%comentário%\\%")]
    [InlineData("#tag e $x$", "\\#tag e \\$x\\$")]
    [InlineData("- não é item", "\\- não é item")]
    [InlineData("1. não é item", "1\\. não é item")]
    [InlineData("a*b_c`d|e~f=g[h]i<j>k\\l^m", "a\\*b\\_c\\`d\\|e\\~f\\=g\\[h\\]i\\<j\\>k\\\\l\\^m")]
    public void Texto_que_o_obsidian_leria_como_marcacao_e_escapado(string text, string expected)
    {
        Assert.Equal(expected + "\n", Markdown(RichDocx(Paragraph(System.Security.SecurityElement.Escape(text)))));
    }

    [Fact]
    public void Negrito_com_espaco_nas_pontas_fica_valido()
    {
        // "** x **" não vira negrito no Markdown: os espaços saem para fora das marcas.
        Assert.Equal("a **b** c\n", Markdown(RichDocx("<w:p><w:r><w:t>a</w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t xml:space=\"preserve\"> b </w:t></w:r><w:r><w:t>c</w:t></w:r></w:p>")));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("\\\\servidor\\pasta")]
    public void Link_fora_de_http_https_e_mailto_vira_texto(string target)
    {
        var markdown = Markdown(RichDocx(
            "<w:p><w:hyperlink r:id=\"rIdL\"><w:r><w:t>clique</w:t></w:r></w:hyperlink></w:p>",
            relations: [("rIdL", "hyperlink", target, true)]));

        Assert.Equal("clique\n", markdown);
    }

    [Fact]
    public void Link_com_espaco_ou_parenteses_fica_entre_sinais()
    {
        var markdown = Markdown(RichDocx(
            "<w:p><w:hyperlink r:id=\"rIdL\"><w:r><w:t>wiki</w:t></w:r></w:hyperlink></w:p>",
            relations: [("rIdL", "hyperlink", "https://exemplo.com/a (b)", true)]));

        Assert.Equal("[wiki](<https://exemplo.com/a (b)>)\n", markdown);
    }

    [Fact]
    public void Codigo_com_crases_usa_cerca_maior()
    {
        var markdown = Markdown(RichDocx(
            "<w:p><w:pPr><w:pStyle w:val=\"C\"/></w:pPr><w:r><w:t>```bash</w:t></w:r></w:p>",
            styles: "<w:style w:type=\"paragraph\" w:styleId=\"C\"><w:name w:val=\"Code\"/></w:style>"));

        Assert.Equal("````\n```bash\n````\n", markdown);
    }

    [Fact]
    public void Quebra_de_linha_e_tabela_com_barra()
    {
        var markdown = Markdown(RichDocx(
            "<w:p><w:r><w:t>um</w:t><w:br/><w:t>dois</w:t></w:r></w:p>"
            + "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>a|b</w:t><w:br/><w:t>c</w:t></w:r></w:p></w:tc></w:tr></w:tbl>"));

        Assert.Equal("um\\\ndois\n\n| a\\|b<br>c |\n| --- |\n", markdown);
    }

    [Fact]
    public void Imagem_externa_nunca_e_buscada()
    {
        var (error, markdown, images) = Convert(RichDocx(
            "<w:p>" + Drawing("rIdI", attribute: "r:link") + "</w:p>",
            relations: [("rIdI", "image", "https://rastreador.exemplo/pixel.png", true)]));

        Assert.Null(error);
        Assert.Equal("\\[imagem externa não incluída\\]\n", markdown);
        Assert.Empty(images);
    }

    [Fact]
    public void Arquivo_que_nao_e_docx_e_corrompido()
    {
        // TST-03.
        Assert.Equal("CORRUPTED", Convert("não é zip"u8.ToArray()).Error);
        Assert.Equal("CORRUPTED", Convert(RichDocx("<w:p><w:r><w:t>sem fim")).Error);
    }

    [Fact]
    public void Imagem_que_descompacta_alem_do_limite_para_sem_terminar()
    {
        // TST-08: uma BMP não gera saída, então quem para é a leitura da mídia.
        var docx = RichDocx(
            "<w:p>" + Drawing("rIdI") + "</w:p>",
            relations: [("rIdI", "image", "media/image1.bmp", false)],
            media: ("word/media/image1.bmp", [(byte)'B', (byte)'M', .. new byte[50_000]]));

        Assert.Equal("LIMIT_EXCEEDED", Convert(docx, new DocxMarkdownConverter(maxPartBytes: 20_000)).Error);
        Assert.Null(Convert(docx, new DocxMarkdownConverter(maxPartBytes: 1_000_000)).Error);
    }
}
