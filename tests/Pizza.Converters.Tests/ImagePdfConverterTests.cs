using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NetVips;
using Pizza.Core.Conversions;
using Image = NetVips.Image;

namespace Pizza.Converters.Tests;

/// <summary>TST-02 para Imagem → PDF: página A4 com margem de 10 mm (ADR-022).</summary>
public class ImagePdfConverterTests
{
    private const double Margin = 28.35;

    [Theory]
    [InlineData(300, 400, 595.28, 841.89)]
    [InlineData(400, 300, 841.89, 595.28)]
    [InlineData(300, 300, 595.28, 841.89)]
    public void Folha_a4_acompanha_a_imagem(int width, int height, double pageWidth, double pageHeight)
    {
        var pdf = Convert(Solid(width, height));

        Assert.Equal((pageWidth, pageHeight), MediaBox(pdf));
    }

    [Theory]
    [InlineData(300, 400)]
    [InlineData(4000, 1000)]
    [InlineData(1000, 4000)]
    public void Imagem_fica_inteira_dentro_das_margens_e_centralizada(int width, int height)
    {
        var pdf = Convert(Solid(width, height));

        var (pageWidth, pageHeight) = MediaBox(pdf);
        var (drawWidth, drawHeight, x, y) = Placement(pdf);
        Assert.True(x >= Margin - 0.01 && y >= Margin - 0.01);
        Assert.True(x + drawWidth <= pageWidth - Margin + 0.01);
        Assert.True(y + drawHeight <= pageHeight - Margin + 0.01);
        Assert.Equal(pageWidth, (2 * x) + drawWidth, 1);
        Assert.Equal(pageHeight, (2 * y) + drawHeight, 1);
        Assert.Equal(width / (double)height, drawWidth / drawHeight, 2);
    }

    [Fact]
    public void Original_nao_reduz_os_pixels()
    {
        Assert.Equal(3000, ImageWidth(Convert(Solid(3000, 2000))));
    }

    [Theory]
    [InlineData("150", 1630, 1640)]
    [InlineData("96", 1042, 1052)]
    public void Dpi_escolhido_reduz_para_a_folha(string resolution, int minimum, int maximum)
    {
        Assert.InRange(ImageWidth(Convert(Solid(3000, 2000), resolution)), minimum, maximum);
    }

    [Fact]
    public void Imagem_pequena_nao_ganha_pixels()
    {
        Assert.Equal(100, ImageWidth(Convert(Solid(100, 100), "150")));
    }

    [Fact]
    public void Indice_do_pdf_aponta_para_os_objetos()
    {
        var pdf = Convert(Solid(64, 48));
        var text = Encoding.Latin1.GetString(pdf);

        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", text, StringComparison.Ordinal);
        var start = int.Parse(Regex.Match(text, @"startxref\n(\d+)\n").Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.Equal("xref", text.Substring(start, 4));

        var entries = Regex.Matches(text[start..], @"^(\d{10}) 00000 n $", RegexOptions.Multiline);
        Assert.Equal(5, entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var offset = int.Parse(entries[i].Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.Equal($"{i + 1} 0 obj", text.Substring(offset, $"{i + 1} 0 obj".Length));
        }
    }

    [Fact]
    public void Imagem_embutida_e_um_jpeg_valido()
    {
        var jpeg = EmbeddedJpeg(Convert(Solid(64, 48)));

        using var image = Image.NewFromBuffer(jpeg);
        Assert.Equal((64, 48), (image.Width, image.Height));
    }

    [Fact]
    public void Transparencia_vira_fundo_branco()
    {
        using var solid = Image.Black(16, 16, bands: 3);
        using var transparent = solid.Bandjoin(0.0).Copy(interpretation: Enums.Interpretation.Srgb);

        var jpeg = EmbeddedJpeg(Convert(transparent));

        using var image = Image.NewFromBuffer(jpeg);
        Assert.True(image.Avg() > 250);
    }

    [Fact]
    public void Perfil_de_cor_e_aplicado_antes_de_ser_descartado()
    {
        // PRIV-31: o PDF declara DeviceRGB, então os pixels precisam já estar em sRGB.
        using var black = Image.Black(64, 64, bands: 3);
        using var srgb = (black + new[] { 40.0, 200.0, 60.0 }).Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);

        var jpeg = EmbeddedJpeg(Convert(srgb.IccTransform("p3", inputProfile: "srgb")));

        using var image = Image.NewFromBuffer(jpeg);
        Assert.InRange(image[0].Avg(), 30, 52);
        Assert.InRange(image[1].Avg(), 190, 210);
    }

    [Fact]
    public void Pdf_nao_tem_bloco_de_autoria()
    {
        // PRIV-30.
        var text = Encoding.Latin1.GetString(Convert(Solid(32, 32)));

        Assert.DoesNotContain("/Info", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/Author", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/Producer", text, StringComparison.Ordinal);
    }

    private static Image Solid(int width, int height)
    {
        using var black = Image.Black(width, height, bands: 3);
        return (black + new[] { 120.0, 130.0, 140.0 }).Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
    }

    private static byte[] Convert(Image image, string? resolution = null)
    {
        using (image)
        {
            var given = resolution is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["resolution"] = resolution };
            var options = ConverterCatalog.ResolveOptions(ConverterCatalog.Find("image.pdf")!, given)!;
            using var input = new MemoryStream(image.PngsaveBuffer(keep: Enums.ForeignKeep.Icc));
            using var output = new MemoryStream();
            Assert.Null(ConverterRegistry.Find("image.pdf")!.Convert(input, output, options));
            return output.ToArray();
        }
    }

    private static double Parse(Group group) => double.Parse(group.Value, CultureInfo.InvariantCulture);

    private static (double Width, double Height) MediaBox(byte[] pdf)
    {
        var match = Regex.Match(Encoding.Latin1.GetString(pdf), @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]");
        return (Parse(match.Groups[1]), Parse(match.Groups[2]));
    }

    private static (double Width, double Height, double X, double Y) Placement(byte[] pdf)
    {
        var match = Regex.Match(Encoding.Latin1.GetString(pdf), @"q ([\d.]+) 0 0 ([\d.]+) ([\d.]+) ([\d.]+) cm /Im0 Do Q");
        return (Parse(match.Groups[1]), Parse(match.Groups[2]), Parse(match.Groups[3]), Parse(match.Groups[4]));
    }

    private static int ImageWidth(byte[] pdf) =>
        int.Parse(Regex.Match(Encoding.Latin1.GetString(pdf), @"/Width (\d+)").Groups[1].Value, CultureInfo.InvariantCulture);

    private static byte[] EmbeddedJpeg(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var length = int.Parse(Regex.Match(text, @"/DCTDecode /Length (\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
        var start = text.IndexOf("stream\n", text.IndexOf("/DCTDecode", StringComparison.Ordinal), StringComparison.Ordinal) + "stream\n".Length;
        return pdf[start..(start + length)];
    }
}
