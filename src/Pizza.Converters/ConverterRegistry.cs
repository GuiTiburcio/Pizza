namespace Pizza.Converters;

/// <summary>Os conversores que o Worker sabe executar.</summary>
public static class ConverterRegistry
{
    public static IReadOnlyList<IConverter> All => Pizza.Core.Conversions.ConverterCatalog.All.Select(c => Find(c.Id)!).ToArray();

    // SEC-26: criar somente a família solicitada; áudio não deve inicializar libvips antes da política.
    public static IConverter? Find(string id) => id switch
    {
        "image.jpg" => new ImageJpegConverter(),
        "image.png" => new ImagePngConverter(),
        "image.webp" => new ImageWebpConverter(),
        "image.avif" => new ImageAvifConverter(),
        "image.pdf" => new ImagePdfConverter(),
        "image.tiff" => new ImageTiffConverter(),
        "image.ico" => new ImageIcoConverter(),
        "image.gif" => new ImageGifConverter(),
        "pdf.split" => new Pdf.PdfSplitConverter(),
        "pdf.rotate" => new Pdf.PdfRotateConverter(),
        "audio.mp3" or "audio.m4a" or "audio.flac" or "audio.wav" => new Audio.AudioConverter(id),
        _ => null,
    };
}
