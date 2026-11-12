using System.Globalization;

namespace Pizza.Converters.Pdf;

/// <summary>Girar (ADR-033): todas as páginas, no sentido escolhido. O PDF sai regravado e sem conteúdo ativo (SEC-76).</summary>
public sealed class PdfRotateConverter : IConverter
{
    public string Id => "pdf.rotate";

    public string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null)
    {
        if (PdfDocument.TryOpen(source, out var document) is { } error)
        {
            return error;
        }

        using (document)
        {
            progress?.Report(30);
            document!.Rotate(int.Parse(options["rotation"], CultureInfo.InvariantCulture));
            document.RemoveActiveContent();
            progress?.Report(60);
            var saved = document.Save(destination);
            progress?.Report(100);
            return saved;
        }
    }
}
