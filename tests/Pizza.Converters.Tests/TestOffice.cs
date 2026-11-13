using System.IO.Compression;
using System.Text;

namespace Pizza.Converters.Tests;

/// <summary>Monta DOCX e XLSX mínimos em memória, só com as partes que o Brotinho lê (ADR-034).</summary>
internal static class TestOffice
{
    private const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static byte[] Zip(params (string Name, string Content)[] entries)
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

    private static string Relationships(params (string Id, string Type, string Target)[] items) =>
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + string.Concat(items.Select(item => $"<Relationship Id=\"{item.Id}\" Type=\"{Rel}/{item.Type}\" Target=\"{item.Target}\"/>"))
        + "</Relationships>";

    /// <summary>DOCX com o corpo dado (o conteúdo de <c>w:body</c>), em <c>word/document.xml</c>.</summary>
    public static byte[] Docx(string body, string documentPart = "word/document.xml") => Zip(
        ("_rels/.rels", Relationships(("rId1", "officeDocument", documentPart))),
        (documentPart, $"<w:document xmlns:w=\"{Word}\"><w:body>{body}</w:body></w:document>"));

    public static string Paragraph(params string[] runs) => "<w:p>" + string.Concat(runs.Select(run => $"<w:r><w:t xml:space=\"preserve\">{run}</w:t></w:r>")) + "</w:p>";

    /// <summary>XLSX com as planilhas dadas (o conteúdo de <c>sheetData</c>), as strings compartilhadas e os estilos.</summary>
    public static byte[] Xlsx(string[] sheets, string[]? shared = null, string styles = "", string extraSheets = "", params (string Name, string Content)[] extraParts)
    {
        var entries = new List<(string, string)>
        {
            ("_rels/.rels", Relationships(("rId1", "officeDocument", "xl/workbook.xml"))),
            ("xl/workbook.xml", $"<workbook xmlns=\"{Main}\" xmlns:r=\"{Rel}\"><sheets>"
                + string.Concat(sheets.Select((_, index) => $"<sheet name=\"P{index + 1}\" sheetId=\"{index + 1}\" r:id=\"rId{index + 1}\"/>"))
                + extraSheets + "</sheets></workbook>"),
            ("xl/_rels/workbook.xml.rels", Relationships([
                .. sheets.Select((_, index) => ($"rId{index + 1}", "worksheet", $"worksheets/sheet{index + 1}.xml")),
                ("rIdS", "sharedStrings", "sharedStrings.xml"),
                ("rIdT", "styles", "styles.xml"),
                ("rIdC", "chartsheet", "chartsheets/sheet1.xml"),
            ])),
            ("xl/sharedStrings.xml", $"<sst xmlns=\"{Main}\">" + string.Concat((shared ?? []).Select(text => $"<si><t xml:space=\"preserve\">{text}</t></si>")) + "</sst>"),
            ("xl/styles.xml", $"<styleSheet xmlns=\"{Main}\">{styles}</styleSheet>"),
        };
        entries.AddRange(sheets.Select((data, index) => ($"xl/worksheets/sheet{index + 1}.xml", $"<worksheet xmlns=\"{Main}\"><sheetData>{data}</sheetData></worksheet>")));
        entries.AddRange(extraParts);
        return Zip([.. entries]);
    }
}
