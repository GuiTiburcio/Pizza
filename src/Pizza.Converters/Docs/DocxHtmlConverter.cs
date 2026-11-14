using System.Text;
using Pizza.Core.Conversions;

namespace Pizza.Converters.Docs;

/// <summary>
/// DOCX → HTML (ADR-034, Pizza Brotinho): um arquivo só, em UTF-8, com títulos, formatação, listas, tabelas
/// (com mesclas), links, notas de rodapé, citações, código e imagens embutidas. A política de conteúdo da página
/// proíbe script e qualquer busca fora dela; links só em http, https e mailto. Imagem fora dos formatos da web
/// vira PNG quando dá (TIFF) ou um aviso no lugar.
/// </summary>
public sealed class DocxHtmlConverter(long maxPartBytes = OfficePackage.MaxPartBytes) : IConverter
{
    private const string Policy = "default-src 'none'; img-src data:; style-src 'unsafe-inline'";

    private const string Style =
        "body{font-family:system-ui,sans-serif;line-height:1.5;max-width:48rem;margin:2rem auto;padding:0 1rem}"
        + "table{border-collapse:collapse}td{border:1px solid #999;padding:.25rem .5rem;vertical-align:top}"
        + "img{max-width:100%}blockquote{border-left:4px solid #ccc;margin-left:0;padding-left:1rem}"
        + "pre{background:#f4f4f4;padding:.5rem;overflow:auto}";

    public string Id => "document.html";

    public string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null) =>
        OfficePackage.Convert(source, maxPartBytes, package =>
        {
            var document = DocxReader.Read(package, TiffToPng);
            using var writer = new StreamWriter(new CappedStream(destination, maxPartBytes), new UTF8Encoding(false), 64 * 1024, leaveOpen: true) { NewLine = "\n" };
            Write(document, writer);
            return null;
        });

    /// <summary>SEC-41 e SEC-45: a TIFF passa pelo conversor de imagens, com a lista de decodificadores e os limites.</summary>
    private static byte[]? TiffToPng(byte[] tiff)
    {
        using var source = new MemoryStream(tiff);
        using var png = new MemoryStream();
        var options = ConverterCatalog.ResolveOptions(ConverterCatalog.Find("image.png")!, new Dictionary<string, string>())!;
        return new ImagePngConverter().Convert(source, png, options) is null ? png.ToArray() : null;
    }

    private static void Write(DocxDocument document, TextWriter writer)
    {
        var title = document.Body.OfType<ParagraphBlock>().FirstOrDefault(block => block.Kind == BlockKind.Heading) is { } heading
            ? string.Concat(heading.Inlines.OfType<TextRun>().Select(run => run.Text))
            : "";
        writer.Write("<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n");
        writer.Write($"<meta http-equiv=\"Content-Security-Policy\" content=\"{Policy}\">\n");
        writer.Write("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        writer.Write($"<title>{Text(title)}</title>\n<style>{Style}</style>\n</head>\n<body>\n");
        Blocks(document.Body, writer);
        if (document.Notes.Count > 0)
        {
            writer.Write("<section class=\"notas\">\n<hr>\n<ol>\n");
            for (var index = 0; index < document.Notes.Count; index++)
            {
                var number = index + 1;
                writer.Write($"<li id=\"nota-{number}\">");
                Cell(document.Notes[index], writer, " ");
                writer.Write($" <a href=\"#ref-{number}\">↩</a></li>\n");
            }

            writer.Write("</ol>\n</section>\n");
        }

        writer.Write("</body>\n</html>\n");
    }

    private static void Blocks(IReadOnlyList<Block> blocks, TextWriter writer)
    {
        for (var index = 0; index < blocks.Count;)
        {
            switch (blocks[index])
            {
                case ParagraphBlock { List: not null }:
                    index = List(blocks, index, writer);
                    continue;
                case ParagraphBlock { Kind: BlockKind.Code }:
                    // Linhas seguidas de código formam um bloco só.
                    writer.Write("<pre><code>");
                    for (var first = true; index < blocks.Count && blocks[index] is ParagraphBlock { Kind: BlockKind.Code, List: null } line; index++, first = false)
                    {
                        writer.Write(first ? "" : "\n");
                        foreach (var inline in line.Inlines)
                        {
                            writer.Write(inline switch
                            {
                                TextRun run => Text(run.Text),
                                LineBreak => "\n",
                                _ => "",
                            });
                        }
                    }

                    writer.Write("</code></pre>\n");
                    continue;
                case ParagraphBlock { Kind: BlockKind.Rule }:
                    writer.Write("<hr>\n");
                    break;
                case ParagraphBlock paragraph:
                    var tag = paragraph.Kind switch
                    {
                        BlockKind.Heading => $"h{paragraph.Level}",
                        BlockKind.Quote => "blockquote",
                        _ => "p",
                    };
                    writer.Write($"<{tag}>");
                    Inlines(paragraph.Inlines, writer);
                    writer.Write($"</{tag}>\n");
                    break;
                case TableBlock table:
                    Table(table, writer);
                    break;
            }

            index++;
        }
    }

    /// <summary>Itens seguidos viram listas aninhadas pelo nível; devolve o índice depois do último item.</summary>
    private static int List(IReadOnlyList<Block> blocks, int index, TextWriter writer)
    {
        var open = new Stack<(int Depth, bool Ordered)>();
        for (; index < blocks.Count && blocks[index] is ParagraphBlock { List: { } item } paragraph; index++)
        {
            while (open.Count > 0 && open.Peek().Depth > item.Depth)
            {
                writer.Write($"</li>\n{Close(open.Pop().Ordered)}\n");
            }

            if (open.Count > 0 && open.Peek().Depth == item.Depth)
            {
                writer.Write("</li>\n");
                if (open.Peek().Ordered != item.Ordered)
                {
                    writer.Write($"{Close(open.Pop().Ordered)}\n");
                }
            }

            if (open.Count == 0 || open.Peek().Depth < item.Depth)
            {
                writer.Write(open.Count > 0 ? "\n" : "");
                writer.Write(item.Ordered ? "<ol>\n" : "<ul>\n");
                open.Push((item.Depth, item.Ordered));
            }

            writer.Write("<li>");
            Inlines(paragraph.Inlines, writer);
        }

        while (open.Count > 0)
        {
            writer.Write($"</li>\n{Close(open.Pop().Ordered)}\n");
        }

        return index;

        static string Close(bool ordered) => ordered ? "</ol>" : "</ul>";
    }

    private static void Table(TableBlock table, TextWriter writer)
    {
        writer.Write("<table>\n");
        foreach (var row in table.Rows)
        {
            writer.Write("<tr>");
            foreach (var cell in row)
            {
                writer.Write("<td");
                writer.Write(cell.ColumnSpan > 1 ? $" colspan=\"{cell.ColumnSpan}\"" : "");
                writer.Write(cell.RowSpan > 1 ? $" rowspan=\"{cell.RowSpan}\"" : "");
                writer.Write(">");
                Cell(cell.Blocks, writer, "<br>");
                writer.Write("</td>");
            }

            writer.Write("</tr>\n");
        }

        writer.Write("</table>\n");
    }

    /// <summary>Conteúdo de célula ou de nota: parágrafos na mesma linha, separados; tabela dentro, como tabela.</summary>
    private static void Cell(IReadOnlyList<Block> blocks, TextWriter writer, string separator)
    {
        var first = true;
        foreach (var block in blocks)
        {
            if (block is TableBlock table)
            {
                Table(table, writer);
                continue;
            }

            writer.Write(first ? "" : separator);
            Inlines(((ParagraphBlock)block).Inlines, writer);
            first = false;
        }
    }

    private static void Inlines(IReadOnlyList<Inline> inlines, TextWriter writer)
    {
        for (var index = 0; index < inlines.Count;)
        {
            if (inlines[index] is TextRun { Link: { } link })
            {
                writer.Write($"<a href=\"{Attribute(link)}\">");
                while (index < inlines.Count && inlines[index] is TextRun { Link: var same } && same == link)
                {
                    index = Write(inlines, index, writer);
                }

                writer.Write("</a>");
            }
            else
            {
                index = Write(inlines, index, writer);
            }
        }
    }

    /// <summary>Escreve um trecho, juntando os textos seguidos com a mesma formatação e o mesmo link.</summary>
    private static int Write(IReadOnlyList<Inline> inlines, int index, TextWriter writer)
    {
        switch (inlines[index])
        {
            case TextRun run:
                var text = new StringBuilder(run.Text);
                while (index + 1 < inlines.Count && inlines[index + 1] is TextRun next && next.Marks == run.Marks && next.Link == run.Link)
                {
                    text.Append(next.Text);
                    index++;
                }

                writer.Write(Marked(Text(text.ToString()), run.Marks));
                break;
            case LineBreak:
                writer.Write("<br>");
                break;
            case Picture { Notice: { } notice }:
                writer.Write(Text(notice));
                break;
            case Picture picture:
                writer.Write($"<img src=\"data:{picture.Mime};base64,{System.Convert.ToBase64String(picture.Data!)}\" alt=\"{Attribute(picture.Alt)}\">");
                break;
            case NoteReference note:
                writer.Write($"<sup id=\"ref-{note.Number}\"><a href=\"#nota-{note.Number}\">{note.Number}</a></sup>");
                break;
        }

        return index + 1;
    }

    private static string Marked(string html, Marks marks)
    {
        foreach (var (mark, tag) in new[] { (Marks.Subscript, "sub"), (Marks.Superscript, "sup"), (Marks.Highlight, "mark"), (Marks.Underline, "u"), (Marks.Strike, "s"), (Marks.Italic, "em"), (Marks.Bold, "strong") })
        {
            if (marks.HasFlag(mark))
            {
                html = $"<{tag}>{html}</{tag}>";
            }
        }

        return html;
    }

    private static string Text(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

    private static string Attribute(string text) => Text(text).Replace("\"", "&quot;", StringComparison.Ordinal);
}
