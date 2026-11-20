using System.Globalization;
using System.Runtime.InteropServices;
using NetVips;
using Pizza.Core.Output;
using Image = NetVips.Image;

namespace Pizza.Converters.Pdf;

/// <summary>
/// Imagens (adendo da ADR-033, prancha 6B-1): o PDFium desenha cada página sobre fundo branco e o encoder de
/// imagens do projeto (libvips) grava JPG ou PNG. A saída é um pacote com uma imagem por página; o residente
/// confere o formato e dá os nomes.
/// </summary>
public sealed class PdfImagesConverter : IConverter
{
    private const long MaxPixels = 100_000_000; // SEC-41
    private const int MaxSide = 65_500; // o limite do JPEG é 65.535

    public string Id => "pdf.images";

    public string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null)
    {
        var dpi = int.Parse(options["resolution"], CultureInfo.InvariantCulture);
        var quality = int.Parse(options["quality"], CultureInfo.InvariantCulture);
        var png = options["format"] == "png";

        if (PdfiumDocument.TryOpen(source, out var document) is { } error)
        {
            return error;
        }

        using (document)
        {
            // SEC-41: todas as páginas são medidas antes de desenhar a primeira; se alguma passar, nada é gravado.
            var sizes = new (int Width, int Height)[document!.PageCount];
            for (var index = 0; index < sizes.Length; index++)
            {
                if (document.PageSize(index) is not { } size || !double.IsFinite(size.Width) || !double.IsFinite(size.Height) || size.Width <= 0 || size.Height <= 0)
                {
                    return "CORRUPTED";
                }

                var width = Math.Max(Math.Round(size.Width * dpi / 72), 1);
                var height = Math.Max(Math.Round(size.Height * dpi / 72), 1);
                if (width > MaxSide || height > MaxSide || width * height > MaxPixels)
                {
                    return "LIMIT_EXCEEDED";
                }

                sizes[index] = ((int)width, (int)height);
            }

            var bundle = new OutputBundle.Writer(destination, sizes.Length);
            for (var index = 0; index < sizes.Length; index++)
            {
                if (Encode(document, index, sizes[index].Width, sizes[index].Height, png, quality) is not { } image)
                {
                    return "CORRUPTED";
                }

                bundle.Add(image);
                progress?.Report(99 * (index + 1) / sizes.Length);
            }

            bundle.Finish();
            return null;
        }
    }

    private static unsafe byte[]? Encode(PdfiumDocument document, int index, int width, int height, bool png, int quality)
    {
        var bytes = (nuint)width * (nuint)height * 4;
        var pixels = (byte*)NativeMemory.Alloc(bytes);
        try
        {
            if (!document.Render(index, pixels, width, height))
            {
                return null;
            }

            // BGRx na memória do PDFium; o libvips quer RGB. A imagem só olha para o buffer, que segue vivo até o fim.
            using var bgrx = Image.NewFromMemory((nint)pixels, bytes, width, height, 4, Enums.BandFormat.Uchar);
            using var blue = bgrx.ExtractBand(0);
            using var green = bgrx.ExtractBand(1);
            using var red = bgrx.ExtractBand(2);
            using var rgb = red.Bandjoin([green, blue]);
            using var tagged = rgb.Copy(interpretation: Enums.Interpretation.Srgb);
            return png ? tagged.PngsaveBuffer(compression: 2) : tagged.JpegsaveBuffer(q: quality);
        }
        finally
        {
            NativeMemory.Free(pixels);
        }
    }
}
