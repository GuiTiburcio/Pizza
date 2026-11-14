namespace Pizza.Converters;

/// <summary>Os conversores que o Worker sabe executar.</summary>
public static class ConverterRegistry
{
    public static IReadOnlyList<IConverter> All { get; } =
    [
        new ImageJpegConverter(), new ImagePngConverter(), new ImageWebpConverter(), new ImageAvifConverter(), new ImagePdfConverter(),
        new ImageTiffConverter(), new ImageIcoConverter(), new ImageGifConverter(),
        new Pdf.PdfMergeConverter(), new Pdf.PdfSplitConverter(), new Pdf.PdfRotateConverter(),
        new Docs.DocxTextConverter(), new Docs.DocxHtmlConverter(), new Docs.XlsxCsvConverter(),
        new Audio.AudioConverter("audio.mp3"), new Audio.AudioConverter("audio.m4a"), new Audio.AudioConverter("audio.flac"), new Audio.AudioConverter("audio.wav"),
    ];

    public static IConverter? Find(string id) => All.FirstOrDefault(converter => converter.Id == id);
}
