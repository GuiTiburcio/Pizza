using System.Text;

namespace Pizza.Converters.Pdf;

/// <summary>
/// Texto (adendo da ADR-033, prancha 6B-1): o texto de cada página, uma após a outra e separadas por uma linha
/// em branco, em UTF-8 com BOM e CRLF. PDF sem nenhum texto (digitalizado) recebe NO_TEXT e nada é gravado.
/// O texto extraído nunca vai para o log (PRIV-16).
/// </summary>
public sealed class PdfTextConverter : IConverter
{
    public string Id => "pdf.text";

    public string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null)
    {
        if (PdfiumDocument.TryOpen(source, out var document) is { } error)
        {
            return error;
        }

        using (document)
        {
            var pages = new List<string>();
            for (var index = 0; index < document!.PageCount; index++)
            {
                var text = Clean(document.Text(index));
                if (text.Length > 0)
                {
                    pages.Add(text);
                }

                progress?.Report(99 * (index + 1) / document.PageCount);
            }

            if (pages.Count == 0)
            {
                return "NO_TEXT";
            }

            var output = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            destination.Write(output.GetPreamble());
            destination.Write(output.GetBytes(string.Join("\r\n\r\n", pages) + "\r\n"));
            return null;
        }
    }

    // Quebras de linha viram CRLF; controles que não são texto saem; as pontas da página são aparadas.
    private static string Clean(string text)
    {
        var lines = new StringBuilder(text.Length);
        foreach (var character in text.Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            if (character == '\n')
            {
                lines.Append("\r\n");
            }
            else if (character == '\t' || !char.IsControl(character))
            {
                lines.Append(character);
            }
        }

        return lines.ToString().Trim();
    }
}
