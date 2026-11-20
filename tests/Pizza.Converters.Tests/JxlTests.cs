using System.Buffers.Binary;
using NetVips;
using Pizza.Converters.Jxl;
using Pizza.Core.Conversions;
using Image = NetVips.Image;

namespace Pizza.Converters.Tests;

/// <summary>ADR-040: JPEG XL pela libjxl, com os limites da aba Imagem (SEC-40, SEC-41) e erros sem derrubar o Worker (TST-03).</summary>
public class JxlTests
{
    [Fact]
    public void Jpeg_para_jxl_sem_perdas_volta_byte_a_byte()
    {
        var jpeg = NoiseJpeg(96, 64);

        var (error, jxl) = Run("image.jxl", jpeg, ("distance", "0"));

        Assert.Null(error);
        Assert.True(JxlCodec.IsJxl(jxl));
        using var rebuilt = new MemoryStream();
        Assert.Null(JxlCodec.ReconstructJpeg(jxl, rebuilt, ImageConverter.MaxSourceBytes));
        Assert.Equal(jpeg, rebuilt.ToArray());
    }

    [Fact]
    public void Jxl_com_perdas_nao_guarda_o_jpeg_original()
    {
        var (_, jxl) = Run("image.jxl", NoiseJpeg(32, 32), ("distance", "1"));

        Assert.Equal("UNSUPPORTED", JxlCodec.ReconstructJpeg(jxl, new MemoryStream(), ImageConverter.MaxSourceBytes));
    }

    [Fact]
    public void Png_para_jxl_com_perdas_decodifica_nas_mesmas_dimensoes_e_fica_menor()
    {
        var png = Noise(160, 120).PngsaveBuffer();

        var (error, jxl) = Run("image.jxl", png, ("distance", "2"));

        Assert.Null(error);
        Assert.True(jxl.Length < png.Length);
        var (decodeError, decoded) = Run("image.png", jxl);
        Assert.Null(decodeError);
        Assert.Equal((160, 120), PngDimensions(decoded));
    }

    [Fact]
    public void Png_para_jxl_sem_perda_devolve_os_mesmos_pixels()
    {
        using var source = Noise(40, 30);
        var (error, jxl) = Run("image.jxl", source.PngsaveBuffer(), ("distance", "0"));
        Assert.Null(error);

        var (_, png) = Run("image.png", jxl);

        using var decoded = Image.NewFromBuffer(png);
        Assert.Equal(0, (source.Cast(Enums.BandFormat.Uchar) - decoded).Abs().Max());
    }

    [Fact]
    public void Transparencia_sobrevive_ao_jxl()
    {
        using var color = Noise(24, 24);
        using var withAlpha = color.Bandjoin(77.0).Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);

        var (_, jxl) = Run("image.jxl", withAlpha.PngsaveBuffer(), ("distance", "0"));
        var (error, png) = Run("image.png", jxl);

        Assert.Null(error);
        using var decoded = Image.NewFromBuffer(png);
        Assert.Equal(4, decoded.Bands);
        Assert.Equal(77, decoded[3].Avg(), 0.5);
    }

    [Fact]
    public void Jxl_vira_cada_formato_da_aba()
    {
        var (_, jxl) = Run("image.jxl", Noise(32, 24).PngsaveBuffer());

        foreach (var id in new[] { "image.jpg", "image.webp", "image.tiff", "image.gif" })
        {
            var (error, output) = Run(id, jxl);
            Assert.True(error is null && output.Length > 0, id);
        }
    }

    [Fact]
    public void Jxl_corrompido_devolve_erro_sem_excecao()
    {
        var (_, jxl) = Run("image.jxl", Noise(64, 64).PngsaveBuffer());
        var random = new Random(7);

        for (var attempt = 0; attempt < 40; attempt++)
        {
            var broken = (byte[])jxl.Clone();
            for (var i = 0; i < 4; i++)
            {
                broken[random.Next(2, broken.Length)] ^= (byte)random.Next(1, 256);
            }

            var (error, _) = Run("image.png", attempt % 2 == 0 ? broken : broken[..(broken.Length / 2)]);

            Assert.Contains(error, new string?[] { null, "CORRUPTED", "UNSUPPORTED", "LIMIT_EXCEEDED" });
        }
    }

    [Fact]
    public void Jxl_truncado_e_corrompido()
    {
        var (_, jxl) = Run("image.jxl", Noise(64, 64).PngsaveBuffer());

        var (error, _) = Run("image.png", jxl[..(jxl.Length / 2)]);

        Assert.Equal("CORRUPTED", error);
    }

    [Fact]
    public void Imagem_acima_do_limite_de_pixels_e_recusada_antes_de_alocar()
    {
        var (_, jxl) = Run("image.jxl", Noise(64, 64).PngsaveBuffer());

        var error = JxlCodec.Decode(jxl, maxPixels: 64 * 64 - 1, out var pixels);

        Assert.Equal("LIMIT_EXCEEDED", error);
        Assert.Null(pixels);
    }

    [Fact]
    public void Jxl_e_reconhecido_pelo_conteudo_e_nao_pela_extensao()
    {
        Assert.False(JxlCodec.IsJxl("P3\n1 1\n255\n0 0 0\n"u8));
        Assert.False(JxlCodec.IsJxl(NoiseJpeg(16, 16)));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("3", true)]
    [InlineData("4", false)]
    [InlineData("0.5", false)]
    public void Distancia_fora_do_esquema_e_recusada(string value, bool accepted)
    {
        var given = new Dictionary<string, string> { ["distance"] = value };

        Assert.Equal(accepted, ConverterCatalog.ResolveOptions(ConverterCatalog.Find("image.jxl")!, given) is not null);
    }

    private static (string? Error, byte[] Output) Run(string converterId, byte[] source, params (string Key, string Value)[] options)
    {
        var info = ConverterCatalog.Find(converterId)!;
        var resolved = ConverterCatalog.ResolveOptions(info, options.ToDictionary(o => o.Key, o => o.Value))!;
        using var input = new MemoryStream(source);
        using var output = new MemoryStream();
        var error = ConverterRegistry.Find(converterId)!.Convert(input, output, resolved);
        return (error, output.ToArray());
    }

    private static Image Noise(int width, int height) =>
        Image.Gaussnoise(width, height, sigma: 40, mean: 128, seed: 3).Cast(Enums.BandFormat.Uchar)
            .Bandjoin([Image.Gaussnoise(width, height, sigma: 40, mean: 100, seed: 4).Cast(Enums.BandFormat.Uchar), Image.Gaussnoise(width, height, sigma: 40, mean: 90, seed: 5).Cast(Enums.BandFormat.Uchar)])
            .Copy(interpretation: Enums.Interpretation.Srgb);

    private static byte[] NoiseJpeg(int width, int height) => Noise(width, height).JpegsaveBuffer(q: 85);

    private static (int Width, int Height) PngDimensions(ReadOnlySpan<byte> png) =>
        (BinaryPrimitives.ReadInt32BigEndian(png[16..]), BinaryPrimitives.ReadInt32BigEndian(png[20..]));
}
