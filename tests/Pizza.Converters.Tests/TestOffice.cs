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

    /// <summary>Prefixos usados nos DOCX completos: w (texto), r (relações), a e wp (desenhos), v (VML antigo).</summary>
    public const string WordNamespaces =
        $"xmlns:w=\"{Word}\" xmlns:r=\"{Rel}\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" "
        + "xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:v=\"urn:schemas-microsoft-com:vml\"";

    /// <summary>
    /// DOCX com estilos, numeração, notas de rodapé, relações do documento e mídias (bytes). As partes vazias
    /// ficam de fora do pacote.
    /// </summary>
    public static byte[] RichDocx(
        string body,
        string styles = "",
        string numbering = "",
        string footnotes = "",
        (string Id, string Type, string Target, bool External)[]? relations = null,
        params (string Name, byte[] Content)[] media)
    {
        var related = (relations ?? []).ToList();
        var entries = new List<(string, byte[])>
        {
            ("_rels/.rels", Utf8(Relationships(("rId1", "officeDocument", "word/document.xml")))),
            ("word/document.xml", Utf8($"<w:document {WordNamespaces}><w:body>{body}</w:body></w:document>")),
        };
        foreach (var (type, name, content) in new[] { ("styles", "styles.xml", styles), ("numbering", "numbering.xml", numbering), ("footnotes", "footnotes.xml", footnotes) })
        {
            if (content.Length > 0)
            {
                related.Add(($"rId{type}", type, name, false));
                entries.Add(($"word/{name}", Utf8($"<w:{type} {WordNamespaces}>{content}</w:{type}>")));
            }
        }

        entries.Add(("word/_rels/document.xml.rels", Utf8(
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
            + string.Concat(related.Select(item => $"<Relationship Id=\"{item.Id}\" Type=\"{Rel}/{item.Type}\" Target=\"{item.Target.Replace("&", "&amp;")}\"{(item.External ? " TargetMode=\"External\"" : "")}/>"))
            + "</Relationships>")));
        entries.AddRange(media);
        return ZipBytes([.. entries]);
    }

    public static byte[] ZipBytes(params (string Name, byte[] Content)[] entries)
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

    private static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    /// <summary>Imagem no texto, como o Word grava: desenho com texto alternativo e a mídia pela relação.</summary>
    public static string Drawing(string relationId, string alt = "", string attribute = "r:embed") =>
        $"<w:r><w:drawing><wp:inline><wp:docPr id=\"1\" name=\"Imagem\" descr=\"{alt}\"/><a:graphic><a:graphicData><a:blip {attribute}=\"{relationId}\"/></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";

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
