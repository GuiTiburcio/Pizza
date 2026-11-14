using System.Globalization;
using System.Text;
using System.Xml;
using Pizza.Core.Output;

namespace Pizza.Converters.Docs;

/// <summary>
/// XLSX → CSV (ADR-034, Pizza Brotinho): um CSV por planilha, na ordem da pasta de trabalho, sem gráficos. O
/// separador, o decimal e as datas seguem as configurações regionais do Windows, como o Excel faz. Fórmulas
/// saem com o último valor calculado. Texto que o Excel leria como fórmula ganha um apóstrofo na frente
/// (injeção de fórmula em CSV). UTF-8 com BOM e CRLF; a saída é um pacote de partes que o residente nomeia.
/// </summary>
public sealed class XlsxCsvConverter(long maxPartBytes = OfficePackage.MaxPartBytes) : IConverter
{
    private const int MaxRow = 1_048_576;
    private const int MaxColumn = 16_384;

    private static readonly string[] Namespaces =
    [
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
        "http://purl.oclc.org/ooxml/spreadsheetml/main",
    ];

    public string Id => "document.csv";

    public string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null) =>
        OfficePackage.Convert(source, maxPartBytes, package =>
        {
            var workbook = package.MainPart();
            var relations = package.Relationships(workbook);
            var (main, sheets, date1904) = Workbook(package, workbook, relations);
            if (sheets.Count == 0)
            {
                return "UNSUPPORTED";
            }

            var shared = Related(relations, "/sharedStrings") is { } sharedPart ? SharedStrings(package, sharedPart, main) : [];
            var dates = Related(relations, "/styles") is { } stylesPart ? DateStyles(package, stylesPart, main) : [];
            var culture = CultureInfo.CurrentCulture;
            var output = new CappedStream(destination, maxPartBytes);
            var bundle = new OutputBundle.Writer(destination, sheets.Count);
            for (var index = 0; index < sheets.Count; index++)
            {
                bundle.Add(() =>
                {
                    output.Write(Encoding.UTF8.Preamble);
                    using var writer = new StreamWriter(output, new UTF8Encoding(false), 64 * 1024, leaveOpen: true);
                    Sheet(package, sheets[index], main, shared, dates, date1904, culture, writer);
                });
                progress?.Report((index + 1) * 100 / sheets.Count);
            }

            bundle.Finish();
            return null;
        });

    private static string? Related(IReadOnlyList<(string Id, string Type, string Target)> relations, string type) =>
        relations.Where(relation => relation.Type.EndsWith(type, StringComparison.Ordinal)).Select(relation => relation.Target).FirstOrDefault();

    /// <summary>As planilhas (só <c>worksheet</c>) na ordem da pasta de trabalho, e se as datas contam de 1904.</summary>
    private static (string Main, List<string> Sheets, bool Date1904) Workbook(OfficePackage package, string part, IReadOnlyList<(string Id, string Type, string Target)> relations)
    {
        using var reader = package.Open(part);
        var main = reader.NamespaceURI;
        if (reader.LocalName != "workbook" || !Namespaces.Contains(main))
        {
            throw new InvalidDataException("Não é uma pasta de trabalho do Excel.");
        }

        var sheets = new List<string>();
        var date1904 = false;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.NamespaceURI != main)
            {
                continue;
            }

            if (reader.LocalName == "workbookPr")
            {
                date1904 = reader.GetAttribute("date1904") is "1" or "true";
            }
            else if (reader.LocalName == "sheet" && RelationId(reader) is { } id)
            {
                sheets.AddRange(relations.Where(relation => relation.Id == id && relation.Type.EndsWith("/worksheet", StringComparison.Ordinal)).Select(relation => relation.Target).Take(1));
            }
        }

        return (main, sheets, date1904);
    }

    private static string? RelationId(XmlReader reader)
    {
        for (var more = reader.MoveToFirstAttribute(); more; more = reader.MoveToNextAttribute())
        {
            if (reader.LocalName == "id" && reader.NamespaceURI.EndsWith("/relationships", StringComparison.Ordinal))
            {
                var id = reader.Value;
                reader.MoveToElement();
                return id;
            }
        }

        reader.MoveToElement();
        return null;
    }

    private static List<string> SharedStrings(OfficePackage package, string part, string main)
    {
        using var reader = package.Open(part);
        var list = new List<string>();
        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == main && reader.LocalName == "si")
            {
                list.Add(Text(reader, main));
                continue;
            }

            reader.Read();
        }

        return list;
    }

    /// <summary>
    /// Texto de um <c>si</c> ou <c>is</c>: os <c>t</c> diretos e das partes formatadas, sem a leitura fonética
    /// (<c>rPh</c>). Deixa o leitor depois do elemento.
    /// </summary>
    private static string Text(XmlReader reader, string main)
    {
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return "";
        }

        var depth = reader.Depth;
        var text = new StringBuilder();
        reader.Read();
        while (!(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == main && reader.LocalName == "rPh")
            {
                reader.Skip();
            }
            else if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == main && reader.LocalName == "t")
            {
                text.Append(reader.ReadElementContentAsString());
            }
            else
            {
                reader.Read();
            }
        }

        reader.Read();
        return text.ToString();
    }

    /// <summary>Para cada estilo de célula (<c>cellXfs</c>), se o formato numérico é de data ou hora.</summary>
    private static List<bool> DateStyles(OfficePackage package, string part, string main)
    {
        using var reader = package.Open(part);
        var custom = new Dictionary<int, string>();
        var styles = new List<bool>();
        var inCellXfs = false;
        while (reader.Read())
        {
            if (reader.NamespaceURI != main)
            {
                continue;
            }

            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "numFmt"
                && int.TryParse(reader.GetAttribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                custom[id] = reader.GetAttribute("formatCode") ?? "";
            }
            else if (reader.LocalName == "cellXfs")
            {
                inCellXfs = reader.NodeType == XmlNodeType.Element && !reader.IsEmptyElement;
            }
            else if (inCellXfs && reader.NodeType == XmlNodeType.Element && reader.LocalName == "xf")
            {
                var format = int.TryParse(reader.GetAttribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
                styles.Add(custom.TryGetValue(format, out var code) ? IsDateFormat(code) : format is (>= 14 and <= 22) or (>= 27 and <= 36) or (>= 45 and <= 47) or (>= 50 and <= 58));
            }
        }

        return styles;
    }

    /// <summary>Formato próprio é de data se, fora de texto entre aspas, cores e escapes, tem d, m, a, h ou s.</summary>
    internal static bool IsDateFormat(string code)
    {
        for (var index = 0; index < code.Length; index++)
        {
            switch (code[index])
            {
                case '"':
                    index = code.IndexOf('"', index + 1) is var end and >= 0 ? end : code.Length;
                    break;
                case '[':
                    index = code.IndexOf(']', index + 1) is var close and >= 0 ? close : code.Length;
                    break;
                case '\\' or '_' or '*':
                    index++;
                    break;
                case 'd' or 'D' or 'm' or 'M' or 'y' or 'Y' or 'h' or 'H' or 's' or 'S':
                    return true;
            }
        }

        return false;
    }

    private static void Sheet(OfficePackage package, string part, string main, List<string> shared, List<bool> dates, bool date1904, CultureInfo culture, TextWriter writer)
    {
        using var reader = package.Open(part);
        if (reader.LocalName != "worksheet" || reader.NamespaceURI != main)
        {
            throw new InvalidDataException("Não é uma planilha.");
        }

        var separator = culture.TextInfo.ListSeparator;
        var nextRow = 1;
        var row = 0;
        var lastColumn = 0;
        while (!reader.EOF)
        {
            if (reader.NamespaceURI != main)
            {
                reader.Read();
                continue;
            }

            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "row")
            {
                row = reader.GetAttribute("r") is { } r ? int.Parse(r, NumberStyles.None, CultureInfo.InvariantCulture) : nextRow;
                if (row < nextRow || row > MaxRow)
                {
                    throw new InvalidDataException("Linha fora de ordem ou do limite.");
                }

                // Linhas vazias no meio continuam como linhas vazias, para a posição não mudar.
                for (; nextRow < row; nextRow++)
                {
                    writer.Write("\r\n");
                }

                lastColumn = 0;
                if (reader.IsEmptyElement)
                {
                    EndRow();
                }
            }
            else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "c")
            {
                var column = reader.GetAttribute("r") is { } reference ? Column(reference) : lastColumn + 1;
                if (column <= lastColumn)
                {
                    throw new InvalidDataException("Célula fora de ordem.");
                }

                for (var gap = column - lastColumn - (lastColumn == 0 ? 1 : 0); gap > 0; gap--)
                {
                    writer.Write(separator);
                }

                lastColumn = column;
                writer.Write(Field(Cell(reader, main, shared, dates, date1904, culture), separator));
                continue;
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "row")
            {
                EndRow();
            }

            reader.Read();
        }

        void EndRow()
        {
            writer.Write("\r\n");
            nextRow = row + 1;
        }
    }

    /// <summary>Coluna de uma referência como "C5" (1 = A), conferindo os limites do Excel.</summary>
    private static int Column(string reference)
    {
        var letters = 0;
        var column = 0;
        while (letters < reference.Length && char.IsAsciiLetter(reference[letters]) && letters < 3)
        {
            column = (column * 26) + (char.ToUpperInvariant(reference[letters]) - 'A' + 1);
            letters++;
        }

        var digits = reference.AsSpan(letters);
        if (letters == 0 || column > MaxColumn || digits.IsEmpty || digits[0] == '0'
            || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var row) || row > MaxRow)
        {
            throw new InvalidDataException("Referência de célula inválida.");
        }

        return column;
    }

    /// <summary>O valor da célula como o CSV leva. Deixa o leitor depois do elemento.</summary>
    private static (string Value, bool IsText) Cell(XmlReader reader, string main, List<string> shared, List<bool> dates, bool date1904, CultureInfo culture)
    {
        var type = reader.GetAttribute("t") ?? "n";
        var style = int.TryParse(reader.GetAttribute("s"), NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : 0;
        string? value = null;
        string? inline = null;
        if (reader.IsEmptyElement)
        {
            reader.Read();
        }
        else
        {
            var depth = reader.Depth;
            reader.Read();
            while (!(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
            {
                if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == main && reader.LocalName == "v")
                {
                    value = reader.ReadElementContentAsString();
                }
                else if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == main && reader.LocalName == "is")
                {
                    inline = Text(reader, main);
                }
                else if (reader.NodeType == XmlNodeType.Element)
                {
                    reader.Skip();
                }
                else
                {
                    reader.Read();
                }
            }

            reader.Read();
        }

        return type switch
        {
            "s" when value is not null => (shared[int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture) is var index && index < shared.Count ? index : throw new InvalidDataException("Texto compartilhado ausente.")], true),
            "inlineStr" => (inline ?? "", true),
            "str" => (value ?? "", true),
            "b" => (value is null ? "" : value == "1" ? "TRUE" : "FALSE", false),
            "n" when value is not null => (Number(double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture), style < dates.Count && dates[style], date1904, culture), false),
            _ => (value ?? "", false),
        };
    }

    private static string Number(double number, bool date, bool date1904, CultureInfo culture)
    {
        var serial = date1904 ? number + 1462 : number;
        if (!date || serial < 0 || serial > 2_958_465)
        {
            return number.ToString(culture);
        }

        var moment = DateTime.FromOADate(serial);
        return serial < 1 ? moment.ToString("t", culture)
            : serial % 1 == 0 ? moment.ToString("d", culture)
            : moment.ToString("g", culture);
    }

    /// <summary>Neutraliza fórmula em texto (OWASP) e põe entre aspas o que tem separador, aspas ou quebra.</summary>
    private static string Field((string Value, bool IsText) cell, string separator)
    {
        var value = cell.IsText && cell.Value.Length > 0 && cell.Value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + cell.Value : cell.Value;
        return value.Contains(separator, StringComparison.Ordinal) || value.AsSpan().IndexOfAny('"', '\r', '\n') >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }
}
