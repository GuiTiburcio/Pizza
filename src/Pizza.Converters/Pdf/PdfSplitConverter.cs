namespace Pizza.Converters.Pdf;

/// <summary>
/// Dividir (ADR-033, prancha 6A-1): "Cada página" vira um PDF por página; "Pela metade", dois. A saída é um
/// pacote com as partes; o residente confere o formato e grava cada uma com o nome que ele mesmo escolhe.
/// </summary>
public sealed class PdfSplitConverter : IConverter
{
    public string Id => "pdf.split";

    public string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null)
    {
        if (PdfDocument.TryOpen(source, out var document) is { } error)
        {
            return error;
        }

        using (document)
        {
            var pages = document!.PageCount;
            if (pages < 2)
            {
                // Um PDF de uma página não tem o que dividir.
                return "UNSUPPORTED";
            }

            Range[] parts = options["mode"] == "half"
                ? [0..((pages + 1) / 2), ((pages + 1) / 2)..pages]
                : [.. Enumerable.Range(0, pages).Select(page => page..(page + 1))];

            document.RemoveActiveContent();
            progress?.Report(30);
            return document.SaveParts(destination, parts, progress);
        }
    }
}
