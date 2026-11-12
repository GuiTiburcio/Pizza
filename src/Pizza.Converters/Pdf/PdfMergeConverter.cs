namespace Pizza.Converters.Pdf;

/// <summary>
/// Juntar (ADR-033, prancha 6A-1): todas as páginas, na ordem em que os arquivos chegam, num PDF só. Se um
/// arquivo falhar, nada sai: o Juntar não entrega um resultado pela metade.
/// </summary>
public sealed class PdfMergeConverter : IMergeConverter
{
    public string Id => "pdf.merge";

    /// <summary>Com um arquivo só, não há o que juntar (FR-30 com a emenda da ADR-033).</summary>
    public string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null) =>
        "UNSUPPORTED";

    public string? Convert(IReadOnlyList<Stream> sources, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null)
    {
        if (sources.Count < 2)
        {
            return "UNSUPPORTED";
        }

        // SEC-44: a memória do Worker vale para a soma dos arquivos, não para cada um.
        if (sources.Sum(source => source.Length) > PdfDocument.MaxSourceBytes)
        {
            return "LIMIT_EXCEEDED";
        }

        var documents = new List<PdfDocument>(sources.Count);
        try
        {
            foreach (var source in sources)
            {
                if (PdfDocument.TryOpen(source, out var document) is { } error)
                {
                    return error;
                }

                documents.Add(document!);
                if (documents.Sum(opened => opened.PageCount) > PdfDocument.MaxPages)
                {
                    return "LIMIT_EXCEEDED";
                }

                document!.RemoveActiveContent();
                progress?.Report(50 * documents.Count / sources.Count);
            }

            var saved = PdfDocument.Merge(documents, destination);
            progress?.Report(100);
            return saved;
        }
        finally
        {
            documents.ForEach(document => document.Dispose());
        }
    }
}
