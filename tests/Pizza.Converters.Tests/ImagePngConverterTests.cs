using System.Buffers.Binary;
using System.Text;
using NetVips;
using Pizza.Converters;
using Image = NetVips.Image;

namespace Pizza.Converters.Tests;

/// <summary>TST-02 e TST-25 para JPG → PNG. SEC-41, SEC-45, PRIV-30 e PRIV-31.</summary>
public class ImagePngConverterTests
{
    [Fact]
    public void Converte_jpg_em_png_com_as_mesmas_dimensoes()
    {
        var (error, png) = Convert(Jpeg(32, 24));

        Assert.Null(error);
        Assert.True(IsPng(png));
        Assert.Equal((32, 24), Dimensions(png));
    }

    [Fact]
    public void Mantem_as_cores()
    {
        var (_, png) = Convert(Jpeg(32, 24, [200, 40, 90]));

        Operation.Block("VipsForeignLoadPng", false);
        using var result = Image.NewFromBuffer(png);
        Assert.InRange(result[0].Avg(), 194, 206);
        Assert.InRange(result[1].Avg(), 34, 46);
        Assert.InRange(result[2].Avg(), 84, 96);
    }

    [Fact]
    public void Foto_girada_sai_na_posicao_certa()
    {
        // PRIV-31: a orientação vira pixels, porque visualizadores de PNG ignoram a marca.
        using var image = Solid(40, 20).Mutate(m => m.Set(GValue.GIntType, "orientation", 6));

        var (error, png) = Convert(image.JpegsaveBuffer(keep: Enums.ForeignKeep.All));

        Assert.Null(error);
        Assert.Equal((20, 40), Dimensions(png));
    }

    [Fact]
    public void Remove_metadados_pessoais()
    {
        // PRIV-30.
        using var image = Solid(16, 16).Mutate(m => m.Set(GValue.GStrType, "exif-ifd0-Artist", "Fulano de Tal"));
        var jpeg = image.JpegsaveBuffer(keep: Enums.ForeignKeep.All);
        Assert.Contains("Fulano", Encoding.Latin1.GetString(jpeg), StringComparison.Ordinal);

        var (_, png) = Convert(jpeg);

        Assert.DoesNotContain(Chunks(png), name => name is "eXIf" or "tEXt" or "iTXt" or "zTXt");
        Assert.DoesNotContain("Fulano", Encoding.Latin1.GetString(png), StringComparison.Ordinal);
    }

    [Fact]
    public void Jpg_cortado_e_corrompido()
    {
        var whole = Jpeg(256, 256);

        var (error, _) = Convert(whole[..(whole.Length / 2)]);

        Assert.Equal("CORRUPTED", error);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"/>")]
    [InlineData("isto não é uma imagem")]
    [InlineData("")]
    public void Formato_fora_da_lista_e_recusado(string content)
    {
        // TST-25: o que vale é o conteúdo, não a extensão.
        var (error, _) = Convert(Encoding.UTF8.GetBytes(content));

        Assert.Equal("UNSUPPORTED", error);
    }

    [Fact]
    public void Png_disfarcado_de_jpg_e_recusado()
    {
        using var image = Solid(8, 8);

        var (error, _) = Convert(image.PngsaveBuffer());

        Assert.Equal("UNSUPPORTED", error);
    }

    [Fact]
    public void Acima_de_100_megapixels_e_recusado_sem_decodificar()
    {
        // SEC-41.
        using var image = Image.Black(12_000, 12_000);

        var (error, png) = Convert(image.JpegsaveBuffer(q: 10));

        Assert.Equal("LIMIT_EXCEEDED", error);
        Assert.Empty(png);
    }

    private static (string? Error, byte[] Png) Convert(byte[] source)
    {
        using var input = new MemoryStream(source);
        using var output = new MemoryStream();
        var error = ImagePngConverter.Convert(input, output);
        return (error, output.ToArray());
    }

    private static byte[] Jpeg(int width, int height, double[]? color = null)
    {
        using var image = Solid(width, height, color);
        return image.JpegsaveBuffer(q: 100);
    }

    private static Image Solid(int width, int height, double[]? color = null)
    {
        using var black = Image.Black(width, height, bands: 3);
        return (black + (color ?? [120.0, 130.0, 140.0])).Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
    }

    private static bool IsPng(byte[] data) =>
        data.AsSpan().StartsWith((byte[])[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

    private static (int Width, int Height) Dimensions(byte[] png) =>
        (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));

    private static List<string> Chunks(byte[] png)
    {
        var names = new List<string>();
        for (var offset = 8; offset < png.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
            names.Add(Encoding.ASCII.GetString(png, offset + 4, 4));
            offset += 12 + length;
        }

        return names;
    }
}
