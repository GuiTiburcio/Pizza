using System.Globalization;
using System.Text;
using NetVips;
using Image = NetVips.Image;

namespace Pizza.Converters;

/// <summary>
/// Uma imagem numa folha A4 (ADR-022). A imagem é sempre recodificada: os bytes do arquivo de origem e
/// os metadados dele não chegam ao PDF (PRIV-30), e o PDF não tem bloco de autoria.
/// </summary>
public sealed class ImagePdfConverter : ImageConverter
{
    private const double A4Short = 595.28;
    private const double A4Long = 841.89;
    private const double Margin = 28.35; // 10 mm, em pontos.
    private const double PointsPerInch = 72;
    private const int JpegQuality = 90;

    public override string Id => "image.pdf";

    protected override void Encode(Image image, Stream destination, IReadOnlyDictionary<string, string> options)
    {
        using var srgb = ToSrgb(image);
        using var opaque = srgb.HasAlpha() ? srgb.Flatten(background: [255.0]) : srgb.Copy();

        var landscape = opaque.Width > opaque.Height;
        var pageWidth = landscape ? A4Long : A4Short;
        var pageHeight = landscape ? A4Short : A4Long;
        var pointsPerPixel = Math.Min((pageWidth - (2 * Margin)) / opaque.Width, (pageHeight - (2 * Margin)) / opaque.Height);
        var drawWidth = opaque.Width * pointsPerPixel;
        var drawHeight = opaque.Height * pointsPerPixel;

        var shrink = options["resolution"] == "original" ? 1 : Math.Min(1, Number(options, "resolution") / PointsPerInch * pointsPerPixel);
        using var sized = shrink < 1 ? opaque.Resize(shrink) : opaque.Copy();
        var jpeg = sized.JpegsaveBuffer(q: JpegQuality, keep: Enums.ForeignKeep.None);

        var content = $"q {N(drawWidth)} 0 0 {N(drawHeight)} {N((pageWidth - drawWidth) / 2)} {N((pageHeight - drawHeight) / 2)} cm /Im0 Do Q\n";
        var writer = new PdfWriter(destination);
        writer.Header();
        writer.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        writer.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        writer.Object(3, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(pageWidth)} {N(pageHeight)}] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>");
        writer.Stream(4, $"/Type /XObject /Subtype /Image /Width {sized.Width} /Height {sized.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode", jpeg);
        writer.Stream(5, string.Empty, Encoding.ASCII.GetBytes(content));
        writer.Finish();
    }

    private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Escreve objetos PDF guardando a posição de cada um, sem depender de <c>Seek</c> no destino.</summary>
    private sealed class PdfWriter(Stream destination)
    {
        private readonly List<long> _offsets = [];
        private long _position;

        public void Header() => Text("%PDF-1.4\n%âãÏÓ\n");

        public void Object(int number, string body)
        {
            _offsets.Add(_position);
            Text($"{number} 0 obj\n{body}\nendobj\n");
        }

        public void Stream(int number, string dictionary, byte[] data)
        {
            _offsets.Add(_position);
            var separator = dictionary.Length == 0 ? string.Empty : " ";
            Text($"{number} 0 obj\n<< {dictionary}{separator}/Length {data.Length} >>\nstream\n");
            destination.Write(data);
            _position += data.Length;
            Text("\nendstream\nendobj\n");
        }

        public void Finish()
        {
            var start = _position;
            var table = new StringBuilder($"xref\n0 {_offsets.Count + 1}\n0000000000 65535 f \n");
            foreach (var offset in _offsets)
            {
                table.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
            }

            table.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {_offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n");
            Text(table.ToString());
        }

        private void Text(string value)
        {
            var bytes = Encoding.Latin1.GetBytes(value);
            destination.Write(bytes);
            _position += bytes.Length;
        }
    }
}
