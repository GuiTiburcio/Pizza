using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Pizza.Converters.Docs;

/// <summary>Formatação de um trecho de texto.</summary>
[Flags]
internal enum Marks
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Strike = 4,
    Underline = 8,
    Highlight = 16,
    Superscript = 32,
    Subscript = 64,
}

internal abstract record Inline;

/// <summary>Texto com formatação; <c>Link</c> é um endereço já aprovado (http, https ou mailto) ou null.</summary>
internal sealed record TextRun(string Text, Marks Marks, string? Link) : Inline;

internal sealed record LineBreak : Inline;

/// <summary>Imagem embutida (<c>Mime</c> e <c>Data</c>) ou, quando não dá para embutir, o aviso que fica no lugar.</summary>
internal sealed record Picture(string? Mime, byte[]? Data, string Alt, string? Notice) : Inline;

internal sealed record NoteReference(int Number) : Inline;

internal enum BlockKind
{
    Paragraph,
    Heading,
    Quote,
    Code,
    Rule,
}

internal abstract record Block;

/// <summary>Parágrafo; <c>Level</c> é o nível do título (1 a 6); <c>List</c>, o item de lista, se for um.</summary>
internal sealed record ParagraphBlock(BlockKind Kind, int Level, IReadOnlyList<Inline> Inlines, ListItem? List) : Block;

internal sealed record ListItem(int Depth, bool Ordered);

/// <summary>Tabela já sem as células de continuação das mesclas verticais.</summary>
internal sealed record TableBlock(IReadOnlyList<IReadOnlyList<TableCell>> Rows) : Block;

internal sealed record TableCell(IReadOnlyList<Block> Blocks, int ColumnSpan, int RowSpan);

internal sealed record DocxDocument(IReadOnlyList<Block> Body, IReadOnlyList<IReadOnlyList<Block>> Notes);

/// <summary>
/// Lê um DOCX para um modelo simples de blocos e trechos, que o HTML e o Markdown desenham (ADR-034, Pizza
/// Brotinho). O corpo é lido em fluxo, um bloco de cada vez; estilos, numeração e notas são partes pequenas.
/// Imagens vinculadas de fora nunca são buscadas (SEC-50); links só viram links em http, https e mailto.
/// </summary>
internal sealed class DocxReader
{
    private static readonly string[] WordNamespaces =
    [
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main",
        "http://purl.oclc.org/ooxml/wordprocessingml/main",
    ];

    private static readonly XNamespace Compatibility = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    private static readonly XNamespace Math = "http://schemas.openxmlformats.org/officeDocument/2006/math";
    private static readonly string[] SafeSchemes = ["http", "https", "mailto"];

    private readonly OfficePackage _package;
    private readonly Func<byte[], byte[]?> _tiffToPng;
    private readonly Dictionary<string, (BlockKind Kind, int Level)> _paragraphStyles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Marks> _characterStyles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<int, bool>> _numbering = new(StringComparer.Ordinal);
    private readonly Dictionary<string, XElement> _footnotes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _noteNumbers = new(StringComparer.Ordinal);
    private readonly List<IReadOnlyList<Block>> _notes = [];
    private Dictionary<string, string> _images = [];
    private IReadOnlyDictionary<string, string> _external = new Dictionary<string, string>();
    private XNamespace _w = WordNamespaces[0];
    private XNamespace _r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private DocxReader(OfficePackage package, Func<byte[], byte[]?> tiffToPng)
    {
        _package = package;
        _tiffToPng = tiffToPng;
    }

    /// <summary><paramref name="tiffToPng"/> converte uma TIFF em PNG, ou devolve null se não conseguir.</summary>
    public static DocxDocument Read(OfficePackage package, Func<byte[], byte[]?> tiffToPng) => new DocxReader(package, tiffToPng).Read();

    private DocxDocument Read()
    {
        var main = _package.MainPart();
        using var reader = _package.Open(main);
        if (reader.LocalName != "document" || !WordNamespaces.Contains(reader.NamespaceURI))
        {
            throw new InvalidDataException("Não é um documento do Word.");
        }

        _w = reader.NamespaceURI;
        if (_w != WordNamespaces[0])
        {
            _r = "http://purl.oclc.org/ooxml/officeDocument/relationships";
        }

        var relations = _package.Relationships(main);
        _external = _package.ExternalTargets(main);
        _images = relations.Where(relation => relation.Type.EndsWith("/image", StringComparison.Ordinal)).DistinctBy(relation => relation.Id).ToDictionary(relation => relation.Id, relation => relation.Target);
        LoadStyles(Related(relations, "/styles"));
        LoadNumbering(Related(relations, "/numbering"));
        LoadFootnotes(Related(relations, "/footnotes"));

        var body = new List<Block>();
        while (reader.Read() && !(reader.NodeType == XmlNodeType.Element && reader.LocalName == "body" && reader.NamespaceURI == _w))
        {
        }

        if (reader.NodeType == XmlNodeType.Element && !reader.IsEmptyElement)
        {
            reader.Read();
            while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "body"))
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    // Um bloco de cada vez na memória, nunca o documento inteiro.
                    Blocks((XElement)XNode.ReadFrom(reader), body);
                }
                else
                {
                    reader.Read();
                }
            }
        }

        return new DocxDocument(body, _notes);
    }

    private static string? Related(IReadOnlyList<(string Id, string Type, string Target)> relations, string type) =>
        relations.Where(relation => relation.Type.EndsWith(type, StringComparison.Ordinal) && relation.Target.Length > 0).Select(relation => relation.Target).FirstOrDefault();

    private XDocument? Load(string? part)
    {
        if (part is null || !_package.Contains(part))
        {
            return null;
        }

        using var reader = _package.Open(part);
        return XDocument.Load(reader);
    }

    private void LoadStyles(string? part)
    {
        foreach (var style in Load(part)?.Root?.Elements(_w + "style") ?? [])
        {
            if (style.Attribute(_w + "styleId")?.Value is not { } id)
            {
                continue;
            }

            var name = (style.Element(_w + "name")?.Attribute(_w + "val")?.Value ?? "").ToLowerInvariant();
            if (style.Attribute(_w + "type")?.Value == "character")
            {
                _characterStyles.TryAdd(id, Apply(Marks.None, style.Element(_w + "rPr")));
            }
            else if (style.Attribute(_w + "type")?.Value == "paragraph")
            {
                var outline = OutlineLevel(style.Element(_w + "pPr"));
                (BlockKind, int) kind = name switch
                {
                    "title" => (BlockKind.Heading, 1),
                    _ when name.StartsWith("heading ", StringComparison.Ordinal) && int.TryParse(name.AsSpan(8), NumberStyles.None, CultureInfo.InvariantCulture, out var level) && level >= 1
                        => (BlockKind.Heading, System.Math.Min(level, 6)),
                    _ when outline is { } value => (BlockKind.Heading, value),
                    "quote" or "intense quote" => (BlockKind.Quote, 0),
                    _ when name.Contains("code", StringComparison.Ordinal) || name == "html preformatted" => (BlockKind.Code, 0),
                    _ => (BlockKind.Paragraph, 0),
                };
                _paragraphStyles.TryAdd(id, kind);
            }
        }
    }

    /// <summary>Nível de tópico 0 a 5 vira título 1 a 6; 6 a 8 ficam como texto.</summary>
    private int? OutlineLevel(XElement? properties) =>
        int.TryParse(properties?.Element(_w + "outlineLvl")?.Attribute(_w + "val")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var level) && level <= 5 ? level + 1 : null;

    private void LoadNumbering(string? part)
    {
        var root = Load(part)?.Root;
        if (root is null)
        {
            return;
        }

        var abstracts = new Dictionary<string, Dictionary<int, bool>>(StringComparer.Ordinal);
        foreach (var definition in root.Elements(_w + "abstractNum"))
        {
            var levels = new Dictionary<int, bool>();
            foreach (var level in definition.Elements(_w + "lvl"))
            {
                if (int.TryParse(level.Attribute(_w + "ilvl")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    levels.TryAdd(index, level.Element(_w + "numFmt")?.Attribute(_w + "val")?.Value is not (null or "bullet" or "none"));
                }
            }

            abstracts.TryAdd(definition.Attribute(_w + "abstractNumId")?.Value ?? "", levels);
        }

        foreach (var number in root.Elements(_w + "num"))
        {
            if (number.Attribute(_w + "numId")?.Value is { } id && number.Element(_w + "abstractNumId")?.Attribute(_w + "val")?.Value is { } definition
                && abstracts.TryGetValue(definition, out var levels))
            {
                _numbering.TryAdd(id, levels);
            }
        }
    }

    private void LoadFootnotes(string? part)
    {
        foreach (var note in Load(part)?.Root?.Elements(_w + "footnote") ?? [])
        {
            // Os separadores do Word também são "notas", mas sem texto da pessoa.
            if (note.Attribute(_w + "type") is null && note.Attribute(_w + "id")?.Value is { } id)
            {
                _footnotes.TryAdd(id, note);
            }
        }
    }

    private void Blocks(XElement element, List<Block> blocks)
    {
        if (element.Name == _w + "p")
        {
            Paragraph(element, blocks);
        }
        else if (element.Name == _w + "tbl")
        {
            blocks.Add(Table(element));
        }
        else if (element.Name != Compatibility + "Fallback" && element.Name != _w + "sectPr" && element.Name != _w + "del")
        {
            // Controles de conteúdo, XML próprio e conteúdo alternativo: o que interessa está dentro.
            foreach (var child in element.Elements())
            {
                Blocks(child, blocks);
            }
        }
    }

    private void Paragraph(XElement paragraph, List<Block> blocks)
    {
        var properties = paragraph.Element(_w + "pPr");
        var (kind, level) = properties?.Element(_w + "pStyle")?.Attribute(_w + "val")?.Value is { } style && _paragraphStyles.TryGetValue(style, out var known)
            ? known
            : (BlockKind.Paragraph, 0);
        if (OutlineLevel(properties) is { } outline)
        {
            (kind, level) = (BlockKind.Heading, outline);
        }

        var inlines = new List<Inline>();
        Inlines(paragraph, Marks.None, null, inlines);
        var empty = inlines.All(inline => inline is TextRun { Text: var text } && string.IsNullOrWhiteSpace(text));
        if (empty && properties?.Element(_w + "pBdr")?.Element(_w + "bottom") is not null)
        {
            blocks.Add(new ParagraphBlock(BlockKind.Rule, 0, [], null));
            return;
        }

        var list = properties?.Element(_w + "numPr") is { } numbering
            && numbering.Element(_w + "numId")?.Attribute(_w + "val")?.Value is { } id && _numbering.TryGetValue(id, out var levels)
            ? Item(numbering, levels)
            : null;
        if (empty && list is null && kind != BlockKind.Code)
        {
            return;
        }

        blocks.Add(new ParagraphBlock(kind, level, inlines, list));
    }

    private ListItem Item(XElement numbering, Dictionary<int, bool> levels)
    {
        var depth = int.TryParse(numbering.Element(_w + "ilvl")?.Attribute(_w + "val")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? System.Math.Min(value, 8) : 0;
        return new ListItem(depth, levels.GetValueOrDefault(depth));
    }

    private void Inlines(XElement container, Marks marks, string? link, List<Inline> inlines)
    {
        foreach (var child in container.Elements())
        {
            if (child.Name == _w + "r")
            {
                Run(child.Elements(), Apply(marks, child.Element(_w + "rPr")), link, inlines);
            }
            else if (child.Name == _w + "hyperlink")
            {
                Inlines(child, marks, child.Attribute(_r + "id")?.Value is { } id ? SafeLink(id) ?? link : link, inlines);
            }
            else if (child.Name == Math + "t")
            {
                // Equação: por enquanto, só o texto dela.
                inlines.Add(new TextRun(child.Value, marks, link));
            }
            else if (child.Name != _w + "pPr" && child.Name != _w + "del" && child.Name != _w + "moveFrom" && child.Name != Compatibility + "Fallback")
            {
                // Campos simples, inserções, controles de conteúdo, equações e conteúdo alternativo.
                Inlines(child, marks, link, inlines);
            }
        }
    }

    private void Run(IEnumerable<XElement> children, Marks marks, string? link, List<Inline> inlines)
    {
        foreach (var child in children)
        {
            if (child.Name.Namespace == _w)
            {
                switch (child.Name.LocalName)
                {
                    case "t":
                        inlines.Add(new TextRun(child.Value, marks, link));
                        break;
                    case "tab":
                        inlines.Add(new TextRun("\t", marks, link));
                        break;
                    case "noBreakHyphen":
                        inlines.Add(new TextRun("-", marks, link));
                        break;
                    case "br" when child.Attribute(_w + "type")?.Value is null or "textWrapping":
                    case "cr":
                        inlines.Add(new LineBreak());
                        break;
                    case "drawing" or "pict" or "object":
                        Graphic(child, marks, link, inlines);
                        break;
                    case "footnoteReference" when child.Attribute(_w + "id")?.Value is { } id:
                        if (Note(id) is { } number)
                        {
                            inlines.Add(new NoteReference(number));
                        }

                        break;
                }
            }
            else if (child.Name == Compatibility + "AlternateContent")
            {
                // A versão nova (Choice) basta; a antiga (Fallback) repetiria o conteúdo.
                Run(child.Elements(Compatibility + "Choice").Take(1).Elements(), marks, link, inlines);
            }
        }
    }

    /// <summary>Caixa de texto vira texto corrido; desenho com imagem vira a imagem.</summary>
    private void Graphic(XElement graphic, Marks marks, string? link, List<Inline> inlines)
    {
        var boxes = graphic.Descendants(_w + "txbxContent").ToList();
        if (boxes.Count > 0)
        {
            foreach (var paragraph in boxes.Descendants(_w + "p"))
            {
                Inlines(paragraph, marks, link, inlines);
                inlines.Add(new LineBreak());
            }

            return;
        }

        var alt = graphic.Descendants().FirstOrDefault(element => element.Name.LocalName == "docPr")?.Attribute("descr")?.Value ?? "";
        foreach (var image in graphic.Descendants().Where(element => element.Name.LocalName is "blip" or "imagedata"))
        {
            if (image.Attribute(_r + "link")?.Value is not null)
            {
                inlines.Add(new Picture(null, null, alt, "[imagem externa não incluída]"));
                return;
            }

            if ((image.Attribute(_r + "embed") ?? image.Attribute(_r + "id"))?.Value is { } id)
            {
                inlines.Add(Picture(id, alt));
                return;
            }
        }
    }

    private Picture Picture(string id, string alt)
    {
        if (_external.ContainsKey(id))
        {
            return new Picture(null, null, alt, "[imagem externa não incluída]");
        }

        if (!_images.TryGetValue(id, out var part) || !_package.Contains(part))
        {
            return new Picture(null, null, alt, "[imagem não encontrada no arquivo]");
        }

        var data = _package.ReadBytes(part);
        var (mime, format) = Format(data);
        if (format == "TIFF" && _tiffToPng(data) is { } png)
        {
            return new Picture("image/png", png, alt, null);
        }

        return mime is not null
            ? new Picture(mime, data, alt, null)
            : new Picture(null, null, alt, $"[imagem em {format}: salve como PNG no editor e converta de novo]");
    }

    /// <summary>O formato real, pelos primeiros bytes; só os quatro da web saem com tipo.</summary>
    private static (string? Mime, string Format) Format(ReadOnlySpan<byte> data) => data switch
    {
        [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => ("image/png", "PNG"),
        [0xFF, 0xD8, 0xFF, ..] => ("image/jpeg", "JPEG"),
        [(byte)'G', (byte)'I', (byte)'F', (byte)'8', ..] => ("image/gif", "GIF"),
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => ("image/webp", "WEBP"),
        [(byte)'I', (byte)'I', 0x2A, 0x00, ..] or [(byte)'M', (byte)'M', 0x00, 0x2A, ..] => (null, "TIFF"),
        [0x01, 0x00, 0x00, 0x00, ..] when data.Length >= 44 && data[40..44].SequenceEqual(" EMF"u8) => (null, "EMF"),
        [0xD7, 0xCD, 0xC6, 0x9A, ..] or [0x01 or 0x02, 0x00, 0x09, 0x00, ..] => (null, "WMF"),
        [(byte)'B', (byte)'M', ..] => (null, "BMP"),
        _ => (null, "formato desconhecido"),
    };

    /// <summary>Só http, https e mailto viram link; javascript:, file:, caminhos de rede e o resto, não.</summary>
    private string? SafeLink(string id) =>
        _external.TryGetValue(id, out var target) && Uri.TryCreate(target, UriKind.Absolute, out var uri) && SafeSchemes.Contains(uri.Scheme) ? target : null;

    /// <summary>Número da nota pela ordem em que aparece; a nota é lida na primeira referência.</summary>
    private int? Note(string id)
    {
        if (_noteNumbers.TryGetValue(id, out var known))
        {
            return known;
        }

        if (!_footnotes.TryGetValue(id, out var note))
        {
            return null;
        }

        var number = _notes.Count + 1;
        _noteNumbers[id] = number;
        var blocks = new List<Block>();
        _notes.Add(blocks);
        foreach (var child in note.Elements())
        {
            Blocks(child, blocks);
        }

        // O Word põe um espaço depois do número da nota.
        if (blocks.FirstOrDefault() is ParagraphBlock { Inlines: [TextRun first, ..] } paragraph)
        {
            blocks[0] = paragraph with { Inlines = [first with { Text = first.Text.TrimStart() }, .. paragraph.Inlines.Skip(1)] };
        }

        return number;
    }

    private TableBlock Table(XElement table)
    {
        // Mescla vertical: a célula "restart" ganha a altura das "continue" logo abaixo, que somem.
        var grid = new List<List<(int Column, int Span, string? Merge, List<Block> Blocks)>>();
        foreach (var row in table.Elements(_w + "tr"))
        {
            var cells = new List<(int, int, string?, List<Block>)>();
            var column = 0;
            foreach (var cell in row.Elements(_w + "tc"))
            {
                var properties = cell.Element(_w + "tcPr");
                var span = int.TryParse(properties?.Element(_w + "gridSpan")?.Attribute(_w + "val")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= 64 ? value : 1;
                var merge = properties?.Element(_w + "vMerge") is { } vertical ? vertical.Attribute(_w + "val")?.Value ?? "continue" : null;
                var blocks = new List<Block>();
                foreach (var child in cell.Elements().Where(child => child.Name != _w + "tcPr"))
                {
                    Blocks(child, blocks);
                }

                cells.Add((column, span, merge, blocks));
                column += span;
            }

            grid.Add(cells);
        }

        var rows = new List<IReadOnlyList<TableCell>>();
        for (var index = 0; index < grid.Count; index++)
        {
            var cells = new List<TableCell>();
            foreach (var (column, span, merge, blocks) in grid[index])
            {
                if (merge == "continue" && index > 0 && grid[index - 1].Any(above => above.Column == column && above.Merge is not null))
                {
                    continue;
                }

                var rowSpan = 1;
                if (merge == "restart")
                {
                    while (index + rowSpan < grid.Count && grid[index + rowSpan].Any(below => below.Column == column && below.Merge == "continue"))
                    {
                        rowSpan++;
                    }
                }

                cells.Add(new TableCell(blocks, span, rowSpan));
            }

            rows.Add(cells);
        }

        return new TableBlock(rows);
    }

    /// <summary>Aplica as propriedades de um trecho (e o estilo de caractere dele) sobre a formatação herdada.</summary>
    private Marks Apply(Marks marks, XElement? properties)
    {
        if (properties is null)
        {
            return marks;
        }

        if (properties.Element(_w + "rStyle")?.Attribute(_w + "val")?.Value is { } style && _characterStyles.TryGetValue(style, out var styled))
        {
            marks |= styled;
        }

        marks = Toggle(marks, Marks.Bold, properties.Element(_w + "b"));
        marks = Toggle(marks, Marks.Italic, properties.Element(_w + "i"));
        marks = Toggle(marks, Marks.Strike, properties.Element(_w + "strike") ?? properties.Element(_w + "dstrike"));
        marks = Toggle(marks, Marks.Underline, properties.Element(_w + "u"));
        marks = Toggle(marks, Marks.Highlight, properties.Element(_w + "highlight"));
        if (properties.Element(_w + "vertAlign")?.Attribute(_w + "val")?.Value is { } align)
        {
            marks &= ~(Marks.Superscript | Marks.Subscript);
            marks |= align switch
            {
                "superscript" => Marks.Superscript,
                "subscript" => Marks.Subscript,
                _ => Marks.None,
            };
        }

        return marks;
    }

    private Marks Toggle(Marks marks, Marks mark, XElement? element) =>
        element is null ? marks
        : element.Attribute(_w + "val")?.Value is "0" or "false" or "off" or "none" ? marks & ~mark
        : marks | mark;
}
