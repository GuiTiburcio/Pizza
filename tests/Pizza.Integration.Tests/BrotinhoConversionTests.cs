using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Pizza.Core.Conversions;
using Pizza.Core.Ipc;
using Pizza.Core.Output;

namespace Pizza.Integration.Tests;

/// <summary>
/// Pizza Brotinho de ponta a ponta (ADR-034, etapa 3): DOCX → TXT e XLSX → CSV no Worker em AppContainer, com
/// nomes dados pelo residente e o original intacto (TST-02, TST-10).
/// </summary>
[Collection(SandboxCollection.Name)]
public sealed class BrotinhoConversionTests(SandboxFixture fixture) : IDisposable
{
    private const string Rels = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string Office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private readonly string _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pizza-brotinho-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
        File.Delete(_folder + ".journal");
    }

    [Fact]
    public void Docx_vira_txt_ao_lado_sem_tocar_no_original()
    {
        var source = Path.Combine(_folder, "contrato.docx");
        File.WriteAllBytes(source, Zip(
            ("_rels/.rels", $"<Relationships xmlns=\"{Rels}\"><Relationship Id=\"r\" Type=\"{Office}/officeDocument\" Target=\"word/document.xml\"/></Relationships>"),
            ("word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Cláusula 1</w:t></w:r></w:p></w:body></w:document>")));
        var before = SHA256.HashData(File.ReadAllBytes(source));

        var result = Runner().Run(source, "document.txt", "txt");

        Assert.Null(result.ErrorCode);
        Assert.Equal("contrato.txt", result.OutputName);
        Assert.Equal("﻿Cláusula 1\r\n", new UTF8Encoding(false).GetString(File.ReadAllBytes(Path.Combine(_folder, result.OutputName!))));
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(source)));
    }

    [Fact]
    public void Docx_vira_html_com_a_imagem_embutida_sem_script()
    {
        // No Worker em AppContainer: a foto entra no HTML e o HTML sai sem script, ao lado do original.
        var source = Path.Combine(_folder, "relatorio.docx");
        byte[] photo = [0xFF, 0xD8, 0xFF, 0xE0, .. "foto"u8];
        File.WriteAllBytes(source, ZipBytes(
            ("_rels/.rels", Utf8($"<Relationships xmlns=\"{Rels}\"><Relationship Id=\"r\" Type=\"{Office}/officeDocument\" Target=\"word/document.xml\"/></Relationships>")),
            ("word/_rels/document.xml.rels", Utf8($"<Relationships xmlns=\"{Rels}\"><Relationship Id=\"i\" Type=\"{Office}/image\" Target=\"media/foto.jpg\"/></Relationships>")),
            ("word/document.xml", Utf8("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:r=\"" + Office + "\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">"
                + "<w:body><w:p><w:r><w:t>Fachada</w:t></w:r></w:p><w:p><w:r><w:drawing><a:blip r:embed=\"i\"/></w:drawing></w:r></w:p></w:body></w:document>")),
            ("word/media/foto.jpg", photo)));
        var before = SHA256.HashData(File.ReadAllBytes(source));

        var result = Runner().Run(source, "document.html", "html");

        Assert.Null(result.ErrorCode);
        Assert.Equal("relatorio.html", result.OutputName);
        var html = Read(result.OutputName!);
        Assert.Contains("<p>Fachada</p>", html);
        Assert.Contains("<img src=\"data:image/jpeg;base64,", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(source)));
    }

    [Fact]
    public void Xlsx_vira_um_csv_por_planilha_no_formato_regional_do_windows()
    {
        // O Worker em AppContainer usa as mesmas configurações regionais da pessoa (separador e decimal).
        var source = Path.Combine(_folder, "vendas.xlsx");
        File.WriteAllBytes(source, Zip(
            ("_rels/.rels", $"<Relationships xmlns=\"{Rels}\"><Relationship Id=\"r\" Type=\"{Office}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>"),
            ("xl/workbook.xml", $"<workbook xmlns=\"{Main}\" xmlns:r=\"{Office}\"><sheets><sheet name=\"A\" sheetId=\"1\" r:id=\"r1\"/><sheet name=\"B\" sheetId=\"2\" r:id=\"r2\"/></sheets></workbook>"),
            ("xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"{Rels}\"><Relationship Id=\"r1\" Type=\"{Office}/worksheet\" Target=\"worksheets/a.xml\"/><Relationship Id=\"r2\" Type=\"{Office}/worksheet\" Target=\"/xl/worksheets/b.xml\"/></Relationships>"),
            ("xl/worksheets/a.xml", $"<worksheet xmlns=\"{Main}\"><sheetData><row r=\"1\"><c r=\"A1\"><v>1.5</v></c><c r=\"B1\" t=\"inlineStr\"><is><t>x</t></is></c></row></sheetData></worksheet>"),
            ("xl/worksheets/b.xml", $"<worksheet xmlns=\"{Main}\"><sheetData><row r=\"1\"><c r=\"A1\"><v>2</v></c></row></sheetData></worksheet>")));
        var before = SHA256.HashData(File.ReadAllBytes(source));
        var csv = ConverterCatalog.Find("document.csv")!;

        var result = Runner().Run(source, csv.Id, csv.Extension, parts: csv.PartSuffix(new Dictionary<string, string>()));

        Assert.Null(result.ErrorCode);
        Assert.Equal(2, result.Count);
        var culture = CultureInfo.CurrentCulture;
        Assert.Equal($"﻿{1.5.ToString(culture)}{culture.TextInfo.ListSeparator}x\r\n", Read("vendas - planilha 1.csv"));
        Assert.Equal("﻿2\r\n", Read("vendas - planilha 2.csv"));
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(source)));
    }

    [Fact]
    public void Planilha_unica_fica_com_o_nome_do_original()
    {
        var source = Path.Combine(_folder, "caixa.xlsx");
        File.WriteAllBytes(source, Zip(
            ("_rels/.rels", $"<Relationships xmlns=\"{Rels}\"><Relationship Id=\"r\" Type=\"{Office}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>"),
            ("xl/workbook.xml", $"<workbook xmlns=\"{Main}\" xmlns:r=\"{Office}\"><sheets><sheet name=\"A\" sheetId=\"1\" r:id=\"r1\"/></sheets></workbook>"),
            ("xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"{Rels}\"><Relationship Id=\"r1\" Type=\"{Office}/worksheet\" Target=\"worksheets/a.xml\"/></Relationships>"),
            ("xl/worksheets/a.xml", $"<worksheet xmlns=\"{Main}\"><sheetData/></worksheet>")));
        var csv = ConverterCatalog.Find("document.csv")!;

        var result = Runner().Run(source, csv.Id, csv.Extension, parts: csv.PartSuffix(new Dictionary<string, string>()));

        Assert.Null(result.ErrorCode);
        Assert.Equal("caixa.csv", result.OutputName);
        Assert.Equal("﻿", Read("caixa.csv"));
    }

    [Fact]
    public void Docx_falso_nao_gera_saida()
    {
        var source = Path.Combine(_folder, "falso.docx");
        File.WriteAllText(source, "não é um documento");

        var result = Runner().Run(source, "document.txt", "txt");

        Assert.Equal("CORRUPTED", result.ErrorCode);
        Assert.Single(Directory.GetFiles(_folder, "*", new EnumerationOptions { AttributesToSkip = 0 }));
    }

    private string Read(string name) => new UTF8Encoding(false).GetString(File.ReadAllBytes(Path.Combine(_folder, name)));

    private ConversionRunner Runner() =>
        new(fixture.WorkerPath, SandboxFixture.Profile, new TempJournal(_folder + ".journal"), TimeSpan.FromSeconds(60));

    private static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    private static byte[] ZipBytes(params (string Name, byte[] Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }
}
