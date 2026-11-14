using System.Text;
using NetVips;
using Pizza.Converters.Docs;
using static Pizza.Converters.Tests.TestOffice;

namespace Pizza.Converters.Tests;

/// <summary>
/// DOCX → HTML (ADR-034, Pizza Brotinho): um arquivo só, com a estrutura do documento, as imagens embutidas e
/// nenhum script. Arquivo-ouro (TST-02), corrompido (TST-03) e limites (TST-08).
/// </summary>
public sealed class DocxHtmlConverterTests
{
    // PNG de 1×1 pixel.
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    private static readonly byte[] Emf = [1, 0, 0, 0, .. new byte[36], 0x20, 0x45, 0x4D, 0x46, .. new byte[20]];

    private const string Styles =
        "<w:style w:type=\"paragraph\" w:styleId=\"Title\"><w:name w:val=\"Title\"/></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"Ttulo1\"><w:name w:val=\"heading 1\"/></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"Ttulo2\"><w:name w:val=\"heading 2\"/></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"Citao\"><w:name w:val=\"Quote\"/></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"Codigo\"><w:name w:val=\"Code\"/></w:style>"
        + "<w:style w:type=\"character\" w:styleId=\"Forte\"><w:name w:val=\"Strong\"/><w:rPr><w:b/></w:rPr></w:style>";

    private const string Numbering =
        "<w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:numFmt w:val=\"bullet\"/></w:lvl><w:lvl w:ilvl=\"1\"><w:numFmt w:val=\"decimal\"/></w:lvl></w:abstractNum>"
        + "<w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num>";

    private const string AlternateNumbering =
        "<w:abstractNum w:abstractNumId=\"1\"><w:lvl w:ilvl=\"0\"><w:numFmt w:val=\"decimal\"/></w:lvl><w:lvl w:ilvl=\"1\"><w:numFmt w:val=\"bullet\"/></w:lvl></w:abstractNum>"
        + "<w:num w:numId=\"2\"><w:abstractNumId w:val=\"1\"/></w:num>";

    private const string Footnotes =
        "<w:footnote w:type=\"separator\" w:id=\"-1\"><w:p><w:r><w:separator/></w:r></w:p></w:footnote>"
        + "<w:footnote w:id=\"2\"><w:p><w:r><w:footnoteRef/></w:r><w:r><w:t xml:space=\"preserve\"> Fonte </w:t></w:r><w:r><w:rPr><w:i/></w:rPr><w:t>X</w:t></w:r><w:r><w:t>.</w:t></w:r></w:p></w:footnote>";

    private static string Styled(string style, string text) => $"<w:p><w:pPr><w:pStyle w:val=\"{style}\"/></w:pPr><w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r></w:p>";

    private static string Run(string properties, string text) => $"<w:r><w:rPr>{properties}</w:rPr><w:t xml:space=\"preserve\">{text}</w:t></w:r>";

    private static string Item(int level, string text, int number = 1) => $"<w:p><w:pPr><w:numPr><w:ilvl w:val=\"{level}\"/><w:numId w:val=\"{number}\"/></w:numPr></w:pPr><w:r><w:t>{text}</w:t></w:r></w:p>";

    private static string Cell(string properties, string text) => $"<w:tc><w:tcPr>{properties}</w:tcPr>{Paragraph(text)}</w:tc>";

    private static string Html(byte[] docx, IConverter? converter = null) => Convert(docx, converter).Html!;

    private static (string? Error, string? Html) Convert(byte[] docx, IConverter? converter = null)
    {
        using var source = new MemoryStream(docx);
        using var destination = new MemoryStream();
        var error = (converter ?? new DocxHtmlConverter()).Convert(source, destination, new Dictionary<string, string>());
        return (error, error is null ? new UTF8Encoding(false).GetString(destination.ToArray()) : null);
    }

    private static string Body(string html)
    {
        var start = html.IndexOf("<body>\n", StringComparison.Ordinal) + "<body>\n".Length;
        return html[start..html.IndexOf("</body>", StringComparison.Ordinal)];
    }

    [Fact]
    public void Arquivo_ouro_com_a_estrutura_do_documento()
    {
        var docx = RichDocx(
            Styled("Title", "Relatório &amp; \"Plano\"")
            + Styled("Ttulo1", "Introdução")
            + "<w:p><w:r><w:t xml:space=\"preserve\">Texto </w:t></w:r>" + Run("<w:b/>", "negrito") + "<w:r><w:t xml:space=\"preserve\"> e </w:t></w:r>"
            + Run("<w:i/>", "itálico") + "<w:r><w:t xml:space=\"preserve\"> </w:t></w:r>" + Run("<w:strike/>", "tachado") + "<w:r><w:t xml:space=\"preserve\"> </w:t></w:r>"
            + Run("<w:u w:val=\"single\"/>", "sub") + Run("<w:u w:val=\"single\"/>", "linhado") + "<w:r><w:t xml:space=\"preserve\"> </w:t></w:r>"
            + Run("<w:highlight w:val=\"yellow\"/>", "realce") + "<w:r><w:t xml:space=\"preserve\"> H</w:t></w:r>" + Run("<w:vertAlign w:val=\"subscript\"/>", "2")
            + "<w:r><w:t xml:space=\"preserve\">O e x</w:t></w:r>" + Run("<w:vertAlign w:val=\"superscript\"/>", "2") + "<w:r><w:t xml:space=\"preserve\"> </w:t></w:r>"
            + Run("<w:rStyle w:val=\"Forte\"/>", "forte") + Run("<w:b w:val=\"0\"/>", " &lt;script&gt;") + "</w:p>"
            + Styled("Ttulo2", "Links")
            + "<w:p><w:hyperlink r:id=\"rIdL1\"><w:r><w:t>site</w:t></w:r></w:hyperlink><w:r><w:t xml:space=\"preserve\"> </w:t></w:r>"
            + "<w:hyperlink r:id=\"rIdL2\"><w:r><w:t>perigo</w:t></w:r></w:hyperlink><w:r><w:t xml:space=\"preserve\"> </w:t></w:r>"
            + "<w:hyperlink w:anchor=\"_Toc1\"><w:r><w:t>interno</w:t></w:r></w:hyperlink><w:r><w:footnoteReference w:id=\"2\"/></w:r></w:p>"
            + Item(0, "um") + Item(1, "um.a") + Item(1, "um.b") + Item(0, "dois")
            + Styled("Citao", "Citação")
            + Styled("Codigo", "linha 1") + Styled("Codigo", "  linha 2")
            + "<w:p><w:pPr><w:pBdr><w:bottom w:val=\"single\"/></w:pBdr></w:pPr></w:p>"
            + "<w:tbl><w:tblPr/><w:tr>" + Cell("<w:gridSpan w:val=\"2\"/>", "A") + Cell("<w:vMerge w:val=\"restart\"/>", "B") + "</w:tr>"
            + "<w:tr>" + Cell("", "C") + Cell("", "D") + "<w:tc><w:tcPr><w:vMerge/></w:tcPr><w:p/></w:tc></w:tr></w:tbl>"
            + "<w:p>" + Drawing("rIdI1", "logo &quot;x&quot;") + "</w:p>"
            + "<w:p>" + Drawing("rIdI2") + "</w:p>"
            + "<w:p/>",
            Styles,
            Numbering,
            Footnotes,
            [
                ("rIdL1", "hyperlink", "https://exemplo.com/a?b=1&c=2", true),
                ("rIdL2", "hyperlink", "javascript:alert(1)", true),
                ("rIdI1", "image", "media/image1.png", false),
                ("rIdI2", "image", "media/image2.emf", false),
            ],
            ("word/media/image1.png", System.Convert.FromBase64String(Png)),
            ("word/media/image2.emf", Emf));

        var html = Html(docx);

        Assert.StartsWith("<!DOCTYPE html>\n", html);
        Assert.Contains("<title>Relatório &amp; \"Plano\"</title>", html);
        Assert.Equal(
            "<h1>Relatório &amp; \"Plano\"</h1>\n"
            + "<h1>Introdução</h1>\n"
            + "<p>Texto <strong>negrito</strong> e <em>itálico</em> <s>tachado</s> <u>sublinhado</u> <mark>realce</mark> H<sub>2</sub>O e x<sup>2</sup> <strong>forte</strong> &lt;script&gt;</p>\n"
            + "<h2>Links</h2>\n"
            + "<p><a href=\"https://exemplo.com/a?b=1&amp;c=2\">site</a> perigo interno<sup id=\"ref-1\"><a href=\"#nota-1\">1</a></sup></p>\n"
            + "<ul>\n<li>um\n<ol>\n<li>um.a</li>\n<li>um.b</li>\n</ol>\n</li>\n<li>dois</li>\n</ul>\n"
            + "<blockquote>Citação</blockquote>\n"
            + "<pre><code>linha 1\n  linha 2</code></pre>\n"
            + "<hr>\n"
            + "<table>\n<tr><td colspan=\"2\">A</td><td rowspan=\"2\">B</td></tr>\n<tr><td>C</td><td>D</td></tr>\n</table>\n"
            + $"<p><img src=\"data:image/png;base64,{Png}\" alt=\"logo &quot;x&quot;\"></p>\n"
            + "<p>[imagem em EMF: salve como PNG no editor e converta de novo]</p>\n"
            + "<section class=\"notas\">\n<hr>\n<ol>\n<li id=\"nota-1\">Fonte <em>X</em>. <a href=\"#ref-1\">↩</a></li>\n</ol>\n</section>\n",
            Body(html));
    }

    [Fact]
    public void Trocar_marcadores_por_numeros_no_mesmo_nivel_preserva_o_tipo_da_lista()
    {
        // TST-02: a escolha entre marcadores e números faz parte da estrutura do documento.
        var html = Html(RichDocx(
            Item(0, "marcador") + Item(0, "primeiro", 2) + Item(0, "segundo", 2) + Item(0, "outro marcador"),
            numbering: Numbering + AlternateNumbering));

        Assert.Equal(
            "<ul>\n<li>marcador</li>\n</ul>\n"
            + "<ol>\n<li>primeiro</li>\n<li>segundo</li>\n</ol>\n"
            + "<ul>\n<li>outro marcador</li>\n</ul>\n",
            Body(html));
    }

    [Fact]
    public void Trocar_o_tipo_da_sublista_preserva_o_item_pai()
    {
        var html = Html(RichDocx(
            Item(0, "pai") + Item(1, "numerado") + Item(1, "marcado", 2) + Item(0, "seguinte"),
            numbering: Numbering + AlternateNumbering));

        Assert.Equal(
            "<ul>\n<li>pai\n<ol>\n<li>numerado</li>\n</ol>\n"
            + "\n<ul>\n<li>marcado</li>\n</ul>\n</li>\n<li>seguinte</li>\n</ul>\n",
            Body(html));
    }

    [Theory]
    [InlineData(1, "ol")]
    [InlineData(3, "ul")]
    [InlineData(8, "ul")]
    public void Lista_que_comeca_recuada_nao_inventa_itens_vazios(int depth, string tag)
    {
        var html = Html(RichDocx(Item(depth, "primeiro") + Item(depth, "segundo"), numbering: Numbering));

        Assert.Equal($"<{tag}>\n<li>primeiro</li>\n<li>segundo</li>\n</{tag}>\n", Body(html));
    }

    [Fact]
    public void Saltos_de_nivel_na_lista_mantem_o_conteudo_dentro_dos_itens()
    {
        var html = Html(RichDocx(
            Item(0, "pai") + Item(2, "filho") + Item(4, "neto") + Item(2, "irmao") + Item(0, "seguinte"),
            numbering: Numbering));

        Assert.Equal(
            "<ul>\n<li>pai\n<ul>\n<li>filho\n<ul>\n<li>neto</li>\n</ul>\n"
            + "</li>\n<li>irmao</li>\n</ul>\n</li>\n<li>seguinte</li>\n</ul>\n",
            Body(html));
    }

    [Fact]
    public void Codigo_preserva_quebras_e_tabs_dentro_do_mesmo_paragrafo()
    {
        var html = Html(RichDocx(
            "<w:p><w:pPr><w:pStyle w:val=\"Codigo\"/></w:pPr><w:r>"
            + "<w:t>alpha</w:t><w:br/><w:t>beta</w:t><w:cr/><w:t>gamma</w:t><w:tab/><w:t>&lt;tag&gt;</w:t>"
            + "</w:r></w:p>" + Styled("Codigo", "next"),
            styles: Styles));

        Assert.Equal("<pre><code>alpha\nbeta\ngamma\t&lt;tag&gt;\nnext</code></pre>\n", Body(html));
    }

    [Fact]
    public void Html_nao_executa_script_nem_busca_nada_fora()
    {
        // Defesa em profundidade: mesmo se algo escapasse, a política da página não roda script nem busca rede.
        var html = Html(RichDocx(Paragraph("oi")));

        Assert.Contains("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'\">", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("\\\\servidor\\pasta")]
    [InlineData("vbscript:msgbox")]
    [InlineData("data:text/html,oi")]
    public void Link_fora_de_http_https_e_mailto_vira_texto(string target)
    {
        var html = Html(RichDocx(
            "<w:p><w:hyperlink r:id=\"rIdL\"><w:r><w:t>clique</w:t></w:r></w:hyperlink></w:p>",
            relations: [("rIdL", "hyperlink", target, true)]));

        Assert.Equal("<p>clique</p>\n", Body(html));
    }

    [Fact]
    public void Mailto_vira_link()
    {
        var html = Html(RichDocx(
            "<w:p><w:hyperlink r:id=\"rIdL\"><w:r><w:t>escreva</w:t></w:r></w:hyperlink></w:p>",
            relations: [("rIdL", "hyperlink", "mailto:ana@exemplo.com", true)]));

        Assert.Equal("<p><a href=\"mailto:ana@exemplo.com\">escreva</a></p>\n", Body(html));
    }

    [Fact]
    public void Tiff_vira_png_embutido()
    {
        // SEC-45: a TIFF passa pelo mesmo decodificador e pelos mesmos limites das imagens.
        var tiff = Image.Black(3, 2).TiffsaveBuffer();
        var html = Html(RichDocx(
            "<w:p>" + Drawing("rIdI") + "</w:p>",
            relations: [("rIdI", "image", "media/image1.tif", false)],
            media: ("word/media/image1.tif", tiff)));

        const string prefix = "<p><img src=\"data:image/png;base64,";
        var body = Body(html);
        Assert.StartsWith(prefix, body);
        var png = System.Convert.FromBase64String(body[prefix.Length..body.IndexOf('"', prefix.Length)]);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);
    }

    [Theory]
    [InlineData(new byte[] { 0xD7, 0xCD, 0xC6, 0x9A, 0, 0 }, "WMF")]
    [InlineData(new byte[] { (byte)'B', (byte)'M', 0, 0, 0, 0 }, "BMP")]
    [InlineData(new byte[] { 1, 2, 3, 4, 5, 6 }, "formato desconhecido")]
    public void Imagem_sem_formato_da_web_vira_aviso_no_lugar(byte[] content, string format)
    {
        // O formato vem do conteúdo, não da extensão: um .png que não é PNG também cai aqui.
        var html = Html(RichDocx(
            "<w:p>" + Drawing("rIdI") + "</w:p>",
            relations: [("rIdI", "image", "media/image1.png", false)],
            media: ("word/media/image1.png", content)));

        Assert.Equal($"<p>[imagem em {format}: salve como PNG no editor e converta de novo]</p>\n", Body(html));
    }

    [Fact]
    public void Imagem_externa_nunca_e_buscada()
    {
        // SEC-50: nenhuma rede. Imagem vinculada (r:link) aponta para fora do arquivo.
        var html = Html(RichDocx(
            "<w:p>" + Drawing("rIdI", attribute: "r:link") + "</w:p>",
            relations: [("rIdI", "image", "https://rastreador.exemplo/pixel.png", true)]));

        Assert.Equal("<p>[imagem externa não incluída]</p>\n", Body(html));
    }

    [Fact]
    public void Imagem_vml_antiga_tambem_e_embutida()
    {
        var html = Html(RichDocx(
            "<w:p><w:r><w:pict><v:shape><v:imagedata r:id=\"rIdI\" o:title=\"\" xmlns:o=\"urn:schemas-microsoft-com:office:office\"/></v:shape></w:pict></w:r></w:p>",
            relations: [("rIdI", "image", "media/image1.png", false)],
            media: ("word/media/image1.png", System.Convert.FromBase64String(Png))));

        Assert.Equal($"<p><img src=\"data:image/png;base64,{Png}\" alt=\"\"></p>\n", Body(html));
    }

    [Fact]
    public void Texto_apagado_e_codigo_de_campo_ficam_de_fora()
    {
        var html = Html(RichDocx(
            "<w:p><w:del><w:r><w:delText>velho</w:delText></w:r><w:r><w:t>fantasma</w:t></w:r></w:del><w:ins><w:r><w:t>novo</w:t></w:r></w:ins>"
            + "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText> PAGE </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>3</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>"));

        Assert.Equal("<p>novo3</p>\n", Body(html));
    }

    [Fact]
    public void Arquivo_que_nao_e_docx_e_corrompido()
    {
        // TST-03.
        Assert.Equal("CORRUPTED", Convert("não é zip"u8.ToArray()).Error);
        Assert.Equal("CORRUPTED", Convert(Xlsx(["<row r=\"1\"/>"])).Error);
        Assert.Equal("CORRUPTED", Convert(RichDocx("<w:p><w:r><w:t>sem fim")).Error);
    }

    [Fact]
    public void Imagem_que_descompacta_alem_do_limite_para_sem_terminar()
    {
        // TST-08: o teto vale também para as mídias, não só para o XML. Uma BMP não gera saída, então quem para é a leitura.
        var docx = RichDocx(
            "<w:p>" + Drawing("rIdI") + "</w:p>",
            relations: [("rIdI", "image", "media/image1.png", false)],
            media: ("word/media/image1.png", [(byte)'B', (byte)'M', .. new byte[50_000]]));

        Assert.Equal("LIMIT_EXCEEDED", Convert(docx, new DocxHtmlConverter(maxPartBytes: 20_000)).Error);
        Assert.Null(Convert(docx, new DocxHtmlConverter(maxPartBytes: 1_000_000)).Error);
    }
}
