using System.Text;
using Pizza.Core.Conversions;
using Pizza.Core.Output;

namespace Pizza.Converters.Docs;

/// <summary>
/// DOCX → Markdown no jeito do Obsidian (ADR-034 e ADR-038): o texto num .md e as imagens em arquivos ao lado,
/// citadas por <c>![[…]]</c>. Títulos, negrito, itálico, tachado, ==realce==, sublinhado, sobrescrito e
/// subscrito, listas aninhadas, tabelas, links, notas [^1], citações, código e linha horizontal. A saída é um
/// pacote com formatos: o Worker não sabe o nome final das imagens e escreve um marcador (U+E000, número, U+E000)
/// que o residente troca. Texto que o Obsidian leria como marcação é escapado.
/// </summary>
public sealed class DocxMarkdownConverter(long maxPartBytes = OfficePackage.MaxPartBytes) : IConverter
{
    private const char Marker = '';

    // CommonMark aceita escapar qualquer pontuação ASCII; estes têm sentido no Obsidian (inclusive # de etiqueta e $ de fórmula).
    private const string Special = "\\`*_[]<>|~=$^#";

    public string Id => "document.md";

    public string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null) =>
        OfficePackage.Convert(source, maxPartBytes, package =>
        {
            var document = DocxReader.Read(package, TiffToPng);
            var images = new List<Picture>();
            foreach (var picture in Pictures(document.Body.Concat(document.Notes.SelectMany(note => note))))
            {
                if (!images.Any(known => ReferenceEquals(known.Data, picture.Data)))
                {
                    images.Add(picture);
                }
            }

            var output = new CappedStream(destination, maxPartBytes);
            var bundle = new OutputBundle.Writer(destination, 1 + images.Count, formatted: true);
            bundle.Add(PartFormat.Markdown, () =>
            {
                using var writer = new StreamWriter(output, new UTF8Encoding(false), 64 * 1024, leaveOpen: true) { NewLine = "\n" };
                new Renderer(writer, images).Document(document);
            });
            foreach (var image in images)
            {
                bundle.Add(Format(image.Mime!), () => output.Write(image.Data));
            }

            bundle.Finish();
            return null;
        });

    private static byte[]? TiffToPng(byte[] tiff)
    {
        using var source = new MemoryStream(tiff);
        using var png = new MemoryStream();
        var options = ConverterCatalog.ResolveOptions(ConverterCatalog.Find("image.png")!, new Dictionary<string, string>())!;
        return new ImagePngConverter().Convert(source, png, options) is null ? png.ToArray() : null;
    }

    private static PartFormat Format(string mime) => mime switch
    {
        "image/png" => PartFormat.Png,
        "image/jpeg" => PartFormat.Jpeg,
        "image/gif" => PartFormat.Gif,
        _ => PartFormat.Webp,
    };

    /// <summary>As imagens embutíveis, na ordem em que aparecem.</summary>
    private static IEnumerable<Picture> Pictures(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            var found = block switch
            {
                ParagraphBlock paragraph => paragraph.Inlines.OfType<Picture>().Where(picture => picture.Data is not null),
                TableBlock table => Pictures(table.Rows.SelectMany(row => row).SelectMany(cell => cell.Blocks)),
                _ => [],
            };
            foreach (var picture in found)
            {
                yield return picture;
            }
        }
    }

    private sealed class Renderer(TextWriter writer, List<Picture> images)
    {
        public void Document(DocxDocument document)
        {
            var first = true;
            for (var index = 0; index < document.Body.Count;)
            {
                writer.Write(first ? "" : "\n");
                first = false;
                index = Block(document.Body, index);
            }

            for (var index = 0; index < document.Notes.Count; index++)
            {
                writer.Write(first || index > 0 ? "" : "\n");
                writer.Write($"[^{index + 1}]: {Joined(document.Notes[index], " ")}\n");
            }
        }

        /// <summary>Escreve o bloco no índice (uma lista ou um código inteiro contam como um) e devolve o próximo.</summary>
        private int Block(IReadOnlyList<Block> blocks, int index)
        {
            switch (blocks[index])
            {
                case ParagraphBlock { List: not null }:
                    var numbers = new int[9];
                    for (; index < blocks.Count && blocks[index] is ParagraphBlock { List: { } item } paragraph; index++)
                    {
                        Array.Clear(numbers, item.Depth + 1, numbers.Length - item.Depth - 1);
                        var bullet = item.Ordered ? $"{++numbers[item.Depth]}." : "-";
                        writer.Write($"{new string('\t', item.Depth)}{bullet} {Inlines(paragraph.Inlines)}\n");
                    }

                    return index;
                case ParagraphBlock { Kind: BlockKind.Code }:
                    var lines = new List<string>();
                    for (; index < blocks.Count && blocks[index] is ParagraphBlock { Kind: BlockKind.Code, List: null } line; index++)
                    {
                        lines.Add(Clean(string.Concat(line.Inlines.OfType<TextRun>().Select(run => run.Text))));
                    }

                    // A cerca precisa ser maior que qualquer sequência de crases do código.
                    var fence = new string('`', Math.Max(3, LongestRun(lines, '`') + 1));
                    writer.Write($"{fence}\n{string.Join('\n', lines)}\n{fence}\n");
                    return index;
                case ParagraphBlock { Kind: BlockKind.Rule }:
                    writer.Write("---\n");
                    break;
                case ParagraphBlock { Kind: BlockKind.Heading } heading:
                    writer.Write($"{new string('#', heading.Level)} {Inlines(heading.Inlines)}\n");
                    break;
                case ParagraphBlock { Kind: BlockKind.Quote } quote:
                    writer.Write($"> {Inlines(quote.Inlines)}\n");
                    break;
                case ParagraphBlock paragraph:
                    writer.Write($"{LineStart(Inlines(paragraph.Inlines))}\n");
                    break;
                case TableBlock table:
                    Table(table);
                    break;
            }

            return index + 1;
        }

        /// <summary>Tabela do GFM: a primeira linha é o cabeçalho; mescla vira células vazias, que o Markdown não tem.</summary>
        private void Table(TableBlock table)
        {
            var cells = new Dictionary<(int Row, int Column), string>();
            var taken = new HashSet<(int, int)>();
            var width = 0;
            for (var row = 0; row < table.Rows.Count; row++)
            {
                var column = 0;
                foreach (var cell in table.Rows[row])
                {
                    while (taken.Contains((row, column)))
                    {
                        column++;
                    }

                    cells[(row, column)] = Joined(cell.Blocks, "<br>");
                    for (var down = 0; down < cell.RowSpan; down++)
                    {
                        for (var across = 0; across < cell.ColumnSpan; across++)
                        {
                            taken.Add((row + down, column + across));
                        }
                    }

                    column += cell.ColumnSpan;
                    width = Math.Max(width, column);
                }
            }

            for (var row = 0; row < table.Rows.Count; row++)
            {
                writer.Write("| " + string.Join(" | ", Enumerable.Range(0, width).Select(column => cells.GetValueOrDefault((row, column), ""))) + " |\n");
                if (row == 0)
                {
                    writer.Write("|" + string.Concat(Enumerable.Repeat(" --- |", width)) + "\n");
                }
            }
        }

        /// <summary>Conteúdo de célula ou de nota numa linha só.</summary>
        private string Joined(IReadOnlyList<Block> blocks, string separator) =>
            string.Join(separator, blocks.Select(block => block switch
            {
                ParagraphBlock paragraph => Inlines(paragraph.Inlines, separator == "<br>" ? "<br>" : " "),
                TableBlock table => string.Join(" ", table.Rows.SelectMany(row => row).Select(cell => Joined(cell.Blocks, " "))),
                _ => "",
            }));

        private string Inlines(IReadOnlyList<Inline> inlines, string lineBreak = "\\\n")
        {
            var text = new StringBuilder();
            for (var index = 0; index < inlines.Count; index++)
            {
                switch (inlines[index])
                {
                    case TextRun run:
                        var plain = new StringBuilder(run.Text);
                        while (index + 1 < inlines.Count && inlines[index + 1] is TextRun next && next.Marks == run.Marks && next.Link == run.Link)
                        {
                            plain.Append(next.Text);
                            index++;
                        }

                        var marked = Marked(Escape(Clean(plain.ToString())), run.Marks);
                        text.Append(run.Link is { } link ? $"[{marked}]({Destination(link)})" : marked);
                        break;
                    case LineBreak:
                        text.Append(lineBreak);
                        break;
                    case Picture { Notice: { } notice }:
                        text.Append(Escape(notice));
                        break;
                    case Picture picture:
                        text.Append($"![[{Marker}{images.FindIndex(image => ReferenceEquals(image.Data, picture.Data)) + 1}{Marker}]]");
                        break;
                    case NoteReference note:
                        text.Append($"[^{note.Number}]");
                        break;
                }
            }

            return text.ToString();
        }

        /// <summary>As marcas ficam coladas no texto: espaços das pontas saem para fora, senão o Markdown não as reconhece.</summary>
        private static string Marked(string text, Marks marks)
        {
            var core = text.Trim(' ', '\t');
            if (marks == Marks.None || core.Length == 0)
            {
                return text;
            }

            foreach (var (mark, open, close) in new[]
            {
                (Marks.Subscript, "<sub>", "</sub>"), (Marks.Superscript, "<sup>", "</sup>"), (Marks.Underline, "<u>", "</u>"),
                (Marks.Highlight, "==", "=="), (Marks.Strike, "~~", "~~"), (Marks.Italic, "*", "*"), (Marks.Bold, "**", "**"),
            })
            {
                if (marks.HasFlag(mark))
                {
                    core = open + core + close;
                }
            }

            var start = text.Length - text.TrimStart(' ', '\t').Length;
            var end = text.Length - text.TrimEnd(' ', '\t').Length;
            return text[..start] + core + text[(text.Length - end)..];
        }

        private static string Escape(string text)
        {
            var escaped = new StringBuilder(text.Length);
            foreach (var character in text)
            {
                escaped.Append(Special.Contains(character) ? "\\" + character : character);
            }

            // %% abre comentário no Obsidian.
            return escaped.ToString().Replace("%%", "%\\%", StringComparison.Ordinal);
        }

        /// <summary>No começo do parágrafo, "- ", "+ " e "1. " virariam lista.</summary>
        private static string LineStart(string text)
        {
            if (text.StartsWith('-') || text.StartsWith('+'))
            {
                return "\\" + text;
            }

            var digits = text.TakeWhile(char.IsAsciiDigit).Count();
            return digits > 0 && digits < text.Length && text[digits] is '.' or ')' ? text[..digits] + "\\" + text[digits..] : text;
        }

        /// <summary>Endereço com espaço, parênteses ou sinais vai entre &lt; e &gt;.</summary>
        private static string Destination(string link) =>
            link.AsSpan().IndexOfAny(" ()<>") >= 0 ? $"<{link.Replace("<", "%3C", StringComparison.Ordinal).Replace(">", "%3E", StringComparison.Ordinal)}>" : link;

        /// <summary>ADR-038: só o Worker escreve o marcador; o do documento vira o caractere de substituição.</summary>
        private static string Clean(string text) => text.Replace(Marker, '�');

        private static int LongestRun(IEnumerable<string> lines, char character)
        {
            var longest = 0;
            foreach (var line in lines)
            {
                var run = 0;
                foreach (var current in line)
                {
                    run = current == character ? run + 1 : 0;
                    longest = Math.Max(longest, run);
                }
            }

            return longest;
        }
    }
}
