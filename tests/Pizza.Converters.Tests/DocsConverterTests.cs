using System.Globalization;
using System.Text;
using Pizza.Converters.Docs;
using Pizza.Core.Output;
using static Pizza.Converters.Tests.TestOffice;

namespace Pizza.Converters.Tests;

/// <summary>
/// Pizza Brotinho (ADR-034, etapa 3): DOCX → TXT e XLSX → CSV no Worker, só com o .NET (TST-02, TST-08).
/// Texto em UTF-8 com BOM e CRLF, para o Bloco de Notas e o Excel abrirem certo.
/// </summary>
public sealed class DocsConverterTests : IDisposable
{
    private const string Bom = "﻿";
    private readonly string _file = Path.Combine(Path.GetTempPath(), "pizza-docs-" + Guid.NewGuid().ToString("N"));
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public DocsConverterTests() => CultureInfo.CurrentCulture = new CultureInfo("pt-BR");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        File.Delete(_file);
    }

    private static (string? Error, string Text) Text(byte[] docx, IConverter? converter = null)
    {
        using var source = new MemoryStream(docx);
        using var destination = new MemoryStream();
        var error = (converter ?? new DocxTextConverter()).Convert(source, destination, new Dictionary<string, string>());
        return (error, new UTF8Encoding(false).GetString(destination.ToArray()));
    }

    private (string? Error, List<string> Sheets) Csv(byte[] xlsx, IConverter? converter = null)
    {
        using (var source = new MemoryStream(xlsx))
        using (var destination = File.Create(_file))
        {
            var error = (converter ?? new XlsxCsvConverter()).Convert(source, destination, new Dictionary<string, string>());
            if (error is not null)
            {
                return (error, []);
            }
        }

        using var handle = File.OpenHandle(_file);
        var bytes = File.ReadAllBytes(_file);
        var parts = OutputBundle.Read(handle, 2000, "csv");
        return (null, parts.Select(part => new UTF8Encoding(false).GetString(bytes, (int)part.Offset, (int)part.Length)).ToList());
    }

    [Fact]
    public void Docx_vira_texto_com_paragrafos_tabs_quebras_e_tabelas()
    {
        var docx = Docx(
            Paragraph("Olá, ", "mundo")
            + "<w:p><w:pPr><w:tabs><w:tab w:val=\"left\" w:pos=\"720\"/></w:tabs></w:pPr><w:r><w:t>A</w:t><w:tab/><w:t>B</w:t><w:br/><w:t>C</w:t></w:r></w:p>"
            + "<w:p><w:del><w:r><w:delText>apagado</w:delText></w:r></w:del><w:ins><w:r><w:t>novo</w:t></w:r></w:ins><w:r><w:instrText> PAGE </w:instrText></w:r></w:p>"
            + "<w:tbl><w:tr><w:tc>" + Paragraph("x") + "</w:tc><w:tc>" + Paragraph("y") + Paragraph("z") + "</w:tc></w:tr>"
            + "<w:tr><w:tc>" + Paragraph("1") + "</w:tc><w:tc>" + Paragraph("2") + "</w:tc></w:tr></w:tbl>"
            + Paragraph("fim")
            + "<w:sectPr><w:pgSz w:w=\"11906\"/></w:sectPr>");

        var (error, text) = Text(docx);

        Assert.Null(error);
        Assert.Equal(Bom + "Olá, mundo\r\nA\tB\r\nC\r\nnovo\r\nx\ty z\r\n1\t2\r\nfim\r\n", text);
    }

    [Fact]
    public void Caixa_de_texto_com_conteudo_alternativo_aparece_uma_vez()
    {
        // O Word grava a caixa duas vezes: a versão nova (Choice) e a antiga (Fallback).
        const string box = "<w:txbxContent><w:p><w:r><w:t>caixa</w:t></w:r></w:p></w:txbxContent>";
        var docx = Docx(
            "<w:p><w:r><mc:AlternateContent xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\">"
            + $"<mc:Choice Requires=\"wps\"><w:drawing>{box}</w:drawing></mc:Choice><mc:Fallback><w:pict>{box}</w:pict></mc:Fallback>"
            + "</mc:AlternateContent></w:r></w:p>");

        var (error, text) = Text(docx);

        Assert.Null(error);
        Assert.Single(text.Split("caixa")[1..]);
    }

    [Fact]
    public void Documento_principal_e_achado_pelas_relacoes_do_pacote()
    {
        var (error, text) = Text(Docx(Paragraph("outro nome"), "word/document2.xml"));

        Assert.Null(error);
        Assert.Equal(Bom + "outro nome\r\n", text);
    }

    [Fact]
    public void Arquivo_que_nao_e_docx_e_corrompido()
    {
        Assert.Equal("CORRUPTED", Text("não é zip"u8.ToArray()).Error);
        Assert.Equal("CORRUPTED", Text(Zip(("word/document.xml", "<x/>"))).Error); // sem _rels/.rels
        Assert.Equal("CORRUPTED", Text(Zip(("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"/>"))).Error);
    }

    [Fact]
    public void Xml_com_dtd_e_recusado()
    {
        // Sem DTD não há entidade externa nem expansão em cascata (XXE, "billion laughs").
        var docx = Zip(
            ("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"r\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>"),
            ("word/document.xml", "<!DOCTYPE d [<!ENTITY e \"x\">]><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>&e;</w:t></w:r></w:p></w:body></w:document>"));

        Assert.Equal("CORRUPTED", Text(docx).Error);
    }

    [Fact]
    public void Parte_que_descompacta_alem_do_limite_para_sem_terminar()
    {
        // SEC-40, TST-08: o teto vale para os bytes descompactados, não para o tamanho que o zip declara.
        // Muito XML e quase nenhum texto: quem para é a leitura, não a saída.
        var docx = Docx(string.Concat(Enumerable.Repeat("<w:p><w:pPr><w:jc w:val=\"center\"/></w:pPr></w:p>", 2000)));

        Assert.Equal("LIMIT_EXCEEDED", Text(docx, new DocxTextConverter(maxPartBytes: 10_000)).Error);
        Assert.Null(Text(docx, new DocxTextConverter(maxPartBytes: 1_000_000)).Error);
    }

    [Fact]
    public void Planilha_vira_csv_no_formato_do_windows()
    {
        var date = new DateTime(2026, 11, 13).ToOADate().ToString(CultureInfo.InvariantCulture);
        var xlsx = Xlsx(
            [
                "<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\"><v>1234.5</v></c><c r=\"D1\" t=\"inlineStr\"><is><t>ok</t></is></c></row>"
                + $"<row r=\"3\"><c r=\"A3\" t=\"b\"><v>1</v></c><c r=\"B3\" s=\"1\"><v>{date}</v></c><c r=\"C3\" t=\"str\"><f>A1</f><v>calc</v></c><c r=\"D3\" t=\"e\"><v>#DIV/0!</v></c><c r=\"E3\"><v>1E-3</v></c></row>",
            ],
            shared: ["texto; com \"aspas\""],
            styles: "<cellXfs count=\"2\"><xf numFmtId=\"0\"/><xf numFmtId=\"14\"/></cellXfs>");

        var (error, sheets) = Csv(xlsx);

        Assert.Null(error);
        Assert.Equal([Bom + "\"texto; com \"\"aspas\"\"\";1234,5;;ok\r\n\r\nTRUE;13/11/2026;calc;#DIV/0!;0,001\r\n"], sheets);
    }

    [Theory]
    [InlineData("14", "46339", "13/11/2026")]
    [InlineData("22", "46339.5", "13/11/2026 12:00")]
    [InlineData("20", "0.75", "18:00")]
    [InlineData("164", "46339", "13/11/2026")] // formato próprio com d/m/a
    [InlineData("165", "46339", "46339")] // "[Red]0": o d de Red não é dia
    [InlineData("166", "46339", "46339")] // "\"dias\" 0": texto entre aspas não conta
    [InlineData("2", "46339", "46339")]
    public void Datas_seguem_o_formato_da_celula(string format, string value, string expected)
    {
        var xlsx = Xlsx(
            [$"<row r=\"1\"><c r=\"A1\" s=\"1\"><v>{value}</v></c></row>"],
            styles: "<numFmts count=\"3\"><numFmt numFmtId=\"164\" formatCode=\"dd/mm/yyyy\"/><numFmt numFmtId=\"165\" formatCode=\"[Red]0\"/><numFmt numFmtId=\"166\" formatCode=\"&quot;dias&quot; 0\"/></numFmts>"
                + $"<cellXfs count=\"2\"><xf numFmtId=\"0\"/><xf numFmtId=\"{format}\"/></cellXfs>");

        var (error, sheets) = Csv(xlsx);

        Assert.Null(error);
        Assert.Equal(Bom + expected + "\r\n", sheets[0]);
    }

    [Fact]
    public void Texto_que_viraria_formula_no_excel_e_neutralizado()
    {
        // OWASP "CSV injection": =, +, -, @, tab e CR no início de texto. Número negativo continua número.
        var xlsx = Xlsx(
            [
                "<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\" t=\"s\"><v>1</v></c><c r=\"C1\" t=\"s\"><v>2</v></c><c r=\"D1\" t=\"s\"><v>3</v></c>"
                + "<c r=\"E1\" t=\"s\"><v>4</v></c><c r=\"F1\" t=\"s\"><v>5</v></c><c r=\"G1\"><v>-5</v></c><c r=\"H1\" t=\"str\"><f>X</f><v>=1+1</v></c></row>",
            ],
            shared: ["=HYPERLINK(\"x\")", "+1", "-2", "@SUM(A1)", "\tx", "normal"]);

        var (error, sheets) = Csv(xlsx);

        Assert.Null(error);
        Assert.Equal(Bom + "\"'=HYPERLINK(\"\"x\"\")\";'+1;'-2;'@SUM(A1);'\tx;normal;-5;'=1+1\r\n", sheets[0]);
    }

    [Fact]
    public void Cada_planilha_vira_um_csv_na_ordem_da_pasta_de_trabalho_sem_graficos()
    {
        var xlsx = Xlsx(
            ["<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>", "<row r=\"1\"><c r=\"A1\"><v>2</v></c></row>"],
            extraSheets: "<sheet name=\"Gráfico\" sheetId=\"9\" r:id=\"rIdC\"/>");

        var (error, sheets) = Csv(xlsx);

        Assert.Null(error);
        Assert.Equal([Bom + "1\r\n", Bom + "2\r\n"], sheets);
    }

    [Fact]
    public void Pasta_so_com_graficos_nao_tem_o_que_converter()
    {
        var xlsx = Xlsx([], extraSheets: "<sheet name=\"Gráfico\" sheetId=\"9\" r:id=\"rIdC\"/>");

        Assert.Equal("UNSUPPORTED", Csv(xlsx).Error);
    }

    [Theory]
    [InlineData("XFE1")] // coluna 16.385
    [InlineData("A1048577")] // linha 1.048.577
    [InlineData("1A")]
    public void Celula_fora_dos_limites_do_excel_e_recusada(string reference)
    {
        var xlsx = Xlsx([$"<row><c r=\"{reference}\"><v>1</v></c></row>"]);

        Assert.Equal("CORRUPTED", Csv(xlsx).Error);
    }

    [Fact]
    public void Planilha_que_descompacta_alem_do_limite_para_sem_terminar()
    {
        // Muito XML e quase nenhuma saída: quem para é a leitura.
        var xlsx = Xlsx([string.Concat(Enumerable.Range(1, 2000).Select(row => $"<row r=\"{row}\" spans=\"1:1\" ht=\"15\"/>"))]);

        Assert.Equal("LIMIT_EXCEEDED", Csv(xlsx, new XlsxCsvConverter(maxPartBytes: 10_000)).Error);
        Assert.Null(Csv(xlsx, new XlsxCsvConverter(maxPartBytes: 1_000_000)).Error);
    }

    [Fact]
    public void Saida_que_cresceria_alem_do_limite_para_sem_terminar()
    {
        // Poucos bytes de XML viram muitos separadores: uma célula na última coluna, em várias linhas.
        var xlsx = Xlsx([string.Concat(Enumerable.Range(1, 5).Select(row => $"<row r=\"{row}\"><c r=\"XFD{row}\"><v>1</v></c></row>"))]);

        Assert.Equal("LIMIT_EXCEEDED", Csv(xlsx, new XlsxCsvConverter(maxPartBytes: 50_000)).Error);
        Assert.Null(Csv(xlsx, new XlsxCsvConverter(maxPartBytes: 1_000_000)).Error);
    }

    [Fact]
    public void Planilha_que_nao_e_xlsx_e_corrompida()
    {
        Assert.Equal("CORRUPTED", Csv("PK mas não"u8.ToArray()).Error);
        Assert.Equal("CORRUPTED", Csv(Docx(Paragraph("é um docx"))).Error);
    }
}
