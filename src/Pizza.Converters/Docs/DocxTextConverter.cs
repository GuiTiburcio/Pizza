using System.Text;
using System.Xml;

namespace Pizza.Converters.Docs;

/// <summary>
/// DOCX → TXT (ADR-034, Pizza Brotinho): só o corpo do documento, um parágrafo por linha e as células de cada
/// linha de tabela separadas por tab. Sem cabeçalho, rodapé nem notas. UTF-8 com BOM e CRLF, para o Bloco de
/// Notas abrir certo. Texto apagado com controle de alterações e códigos de campo ficam de fora.
/// </summary>
public sealed class DocxTextConverter(long maxPartBytes = OfficePackage.MaxPartBytes) : IConverter
{
    private static readonly string[] Namespaces =
    [
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main",
        "http://purl.oclc.org/ooxml/wordprocessingml/main",
    ];

    private const string Compatibility = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    public string Id => "document.txt";

    public string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null) =>
        OfficePackage.Convert(source, maxPartBytes, package =>
        {
            using var reader = package.Open(package.MainPart());
            if (reader.LocalName != "document" || !Namespaces.Contains(reader.NamespaceURI))
            {
                throw new InvalidDataException("Não é um documento do Word.");
            }

            var output = new CappedStream(destination, maxPartBytes);
            output.Write(Encoding.UTF8.Preamble);
            using var writer = new StreamWriter(output, new UTF8Encoding(false), 64 * 1024, leaveOpen: true) { NewLine = "\r\n" };
            Write(reader, reader.NamespaceURI, writer);
            return null;
        });

    private static void Write(XmlReader reader, string w, TextWriter writer)
    {
        // Tabela de primeiro nível: células com tab e linhas com quebra; dentro dela, o resto vira espaço.
        var tables = 0;
        var firstCell = false;
        var cellHasParagraph = false;
        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == Compatibility && reader.LocalName == "Fallback")
            {
                // O Word grava caixas de texto duas vezes; a versão antiga (Fallback) repetiria o texto.
                reader.Skip();
                continue;
            }

            if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == w)
            {
                switch (reader.LocalName)
                {
                    case "pPr" or "rPr" or "sectPr" or "tblPr" or "trPr" or "tcPr" or "tblGrid":
                        // Propriedades: as marcas de tab daqui são posições, não texto.
                        reader.Skip();
                        continue;
                    case "t":
                        writer.Write(reader.ReadElementContentAsString());
                        continue;
                    case "tab":
                        writer.Write(tables > 0 ? " " : "\t");
                        break;
                    case "br" or "cr":
                        writer.Write(tables > 0 ? " " : writer.NewLine);
                        break;
                    case "tbl" when !reader.IsEmptyElement:
                        tables++;
                        break;
                    case "tr" when tables == 1:
                        firstCell = true;
                        break;
                    case "tc":
                        if (tables > 1 || !firstCell)
                        {
                            writer.Write(tables > 1 ? " " : "\t");
                        }

                        firstCell = false;
                        cellHasParagraph = false;
                        break;
                    case "p" when tables > 0:
                        writer.Write(cellHasParagraph ? " " : "");
                        cellHasParagraph = true;
                        break;
                    case "p" when reader.IsEmptyElement:
                        writer.WriteLine();
                        break;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.NamespaceURI == w)
            {
                switch (reader.LocalName)
                {
                    case "p" when tables == 0:
                    case "tr" when tables == 1:
                        writer.WriteLine();
                        break;
                    case "tbl":
                        tables--;
                        break;
                }
            }

            reader.Read();
        }
    }
}
