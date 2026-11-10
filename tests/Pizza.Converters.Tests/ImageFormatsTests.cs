using System.Buffers.Binary;
using System.Text;
using NetVips;
using Pizza.Core.Conversions;
using Image = NetVips.Image;

namespace Pizza.Converters.Tests;

/// <summary>TST-02, TST-14 e TST-25 para os formatos da aba Imagem (SPECS, SEC-45, PRIV-30).</summary>
public class ImageFormatsTests
{
    public static TheoryData<string> Outputs => new(ConverterRegistry.All.Select(c => c.Id));

    [Fact]
    public void Jpg_tem_a_assinatura_certa()
    {
        var (error, jpg) = Run("image.jpg", Solid(32, 24).PngsaveBuffer());

        Assert.Null(error);
        Assert.Equal((byte[])[0xFF, 0xD8, 0xFF], jpg[..3]);
    }

    [Fact]
    public void Qualidade_maior_gera_arquivo_maior()
    {
        var source = Noise().PngsaveBuffer();

        var (_, maximum) = Run("image.jpg", source, ("quality", "95"));
        var (_, compact) = Run("image.jpg", source, ("quality", "60"));

        Assert.True(maximum.Length > compact.Length);
    }

    [Fact]
    public void Transparencia_vira_fundo_branco_no_jpg()
    {
        using var solid = Solid(16, 16);
        using var transparent = solid.Bandjoin(0.0);

        var (error, jpg) = Run("image.jpg", transparent.Copy(interpretation: Enums.Interpretation.Srgb).PngsaveBuffer());

        Assert.Null(error);
        using var result = Image.NewFromBuffer(jpg);
        Assert.True(result.Avg() > 250);
    }

    [Fact]
    public void Transparencia_de_16_bits_tambem_vira_fundo_branco_no_jpg()
    {
        using var black = Image.Black(16, 16, bands: 3);
        using var color = (black + 30000).Cast(Enums.BandFormat.Ushort);
        using var transparent = color.Bandjoin(0.0).Copy(interpretation: Enums.Interpretation.Rgb16);

        var (error, jpg) = Run("image.jpg", transparent.PngsaveBuffer());

        Assert.Null(error);
        using var result = Image.NewFromBuffer(jpg);
        Assert.True(result.Avg() > 250);
    }

    [Fact]
    public void Webp_tem_a_assinatura_certa()
    {
        var (error, webp) = Run("image.webp", Solid(32, 24).PngsaveBuffer());

        Assert.Null(error);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(webp, 0, 4));
        Assert.Equal("WEBP", Encoding.ASCII.GetString(webp, 8, 4));
    }

    [Fact]
    public void Avif_tem_assinatura_e_dimensoes_certas()
    {
        var (error, avif) = Run("image.avif", Solid(32, 24).PngsaveBuffer());
        Assert.Null(error);
        Assert.Equal("ftyp", Encoding.ASCII.GetString(avif, 4, 4));
        Assert.Contains("avif", Encoding.ASCII.GetString(avif, 8, 24));
        using var result = Image.NewFromBuffer(avif);
        Assert.Equal((32, 24), (result.Width, result.Height));
    }

    [Fact]
    public void Tiff_nao_perde_as_dimensoes()
    {
        var (error, tiff) = Run("image.tiff", Solid(32, 24).PngsaveBuffer());

        Assert.Null(error);
        using var result = Image.NewFromBuffer(tiff);
        Assert.Equal((32, 24), (result.Width, result.Height));
    }

    [Fact]
    public void Gif_tem_a_assinatura_certa()
    {
        var (error, gif) = Run("image.gif", Solid(32, 24).PngsaveBuffer());

        Assert.Null(error);
        Assert.Equal("GIF8", Encoding.ASCII.GetString(gif, 0, 4));
    }

    [Theory]
    [InlineData("256", 0, 256)]
    [InlineData("128", 128, 128)]
    [InlineData("48", 48, 48)]
    public void Ico_tem_o_tamanho_escolhido(string size, byte widthByte, int pixels)
    {
        var (error, ico) = Run("image.ico", Solid(64, 64).PngsaveBuffer(), ("size", size));

        Assert.Null(error);
        Assert.Equal((byte[])[0, 0, 1, 0, 1, 0], ico[..6]);
        Assert.Equal(widthByte, ico[6]);
        Assert.Equal(ico.Length - 22, BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(14)));
        Assert.Equal(22, BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(18)));
        Assert.Equal((pixels, pixels), PngDimensions(ico.AsSpan(22)));
    }

    [Fact]
    public void Gif_aplica_o_perfil_de_cor_antes_de_descartar()
    {
        // PRIV-31: o GIF não guarda perfil, então as cores precisam ser convertidas.
        var (error, gif) = Run("image.gif", WideGamutGreen());

        Assert.Null(error);
        using var result = Image.NewFromBuffer(gif);
        AssertGreen(result);
    }

    [Fact]
    public void Ico_aplica_o_perfil_de_cor_antes_de_descartar()
    {
        var (error, ico) = Run("image.ico", WideGamutGreen(), ("size", "48"));

        Assert.Null(error);
        using var result = Image.NewFromBuffer(ico[22..]);
        AssertGreen(result);
    }

    [Fact]
    public void Imagem_que_nao_e_quadrada_vira_icone_quadrado_sem_distorcer()
    {
        var (_, ico) = Run("image.ico", Solid(64, 32).PngsaveBuffer(), ("size", "48"));

        Assert.Equal((48, 48), PngDimensions(ico.AsSpan(22)));
        using var icon = Image.NewFromBuffer(ico[22..]);
        Assert.Equal(0, icon[3].Getpoint(24, 0)[0]);
        Assert.Equal(255, icon[3].Getpoint(24, 24)[0]);
    }

    public static TheoryData<string, byte[]> Sources()
    {
        using var image = Solid(16, 12);
        return new TheoryData<string, byte[]>
        {
            { "jpg", image.JpegsaveBuffer() },
            { "png", image.PngsaveBuffer() },
            { "webp", image.WebpsaveBuffer() },
            { "tiff", image.TiffsaveBuffer() },
            { "gif", image.GifsaveBuffer() },
            { "avif", image.HeifsaveBuffer(compression: Enums.ForeignHeifCompression.Av1) },
        };
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void Cada_formato_aceito_vira_png(string format, byte[] source)
    {
        var (error, png) = Run("image.png", source);

        Assert.True(error is null, format);
        Assert.Equal((16, 12), PngDimensions(png));
    }

    [Theory]
    [MemberData(nameof(Outputs))]
    public void Saida_nao_leva_metadados_pessoais(string converterId)
    {
        // PRIV-30.
        using var image = Solid(64, 64).Mutate(m => m.Set(GValue.GStrType, "exif-ifd0-Artist", "Fulano de Tal"));
        var jpeg = image.JpegsaveBuffer(keep: Enums.ForeignKeep.All);
        Assert.Contains("Fulano", Encoding.Latin1.GetString(jpeg), StringComparison.Ordinal);

        var (error, output) = Run(converterId, jpeg);

        Assert.Null(error);
        Assert.DoesNotContain("Fulano", Encoding.Latin1.GetString(output), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("image.jpg")]
    [InlineData("image.png")]
    [InlineData("image.webp")]
    public void Manter_metadados_preserva_o_que_o_formato_guarda(string converterId)
    {
        // PRIV-32.
        using var image = Solid(64, 64).Mutate(m => m.Set(GValue.GStrType, "exif-ifd0-Artist", "Fulano de Tal"));
        var jpeg = image.JpegsaveBuffer(keep: Enums.ForeignKeep.All);

        var (error, output) = Run(converterId, jpeg, (ConverterCatalog.MetadataKey, ConverterCatalog.MetadataKeep));

        Assert.Null(error);
        Assert.Contains("Fulano", Encoding.Latin1.GetString(output), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("image.pdf")]
    [InlineData("image.ico")]
    [InlineData("image.tiff")]
    [InlineData("image.gif")]
    public void Formato_sem_a_caixa_recusa_a_opcao_de_metadados(string converterId)
    {
        var given = new Dictionary<string, string> { [ConverterCatalog.MetadataKey] = ConverterCatalog.MetadataKeep };

        Assert.Null(ConverterCatalog.ResolveOptions(ConverterCatalog.Find(converterId)!, given));
    }

    [Theory]
    [InlineData("P3\n1 1\n255\n0 0 0\n")]
    [InlineData("#?RADIANCE\n")]
    public void Decodificador_fora_da_lista_e_recusado(string content)
    {
        // TST-25.
        var (error, _) = Run("image.png", Encoding.ASCII.GetBytes(content));

        Assert.Equal("UNSUPPORTED", error);
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

    private static Image Solid(int width, int height)
    {
        using var black = Image.Black(width, height, bands: 3);
        return (black + new[] { 120.0, 130.0, 140.0 }).Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
    }

    private static byte[] WideGamutGreen()
    {
        using var black = Image.Black(64, 64, bands: 3);
        using var srgb = (black + new[] { 40.0, 200.0, 60.0 }).Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        using var p3 = srgb.IccTransform("p3", inputProfile: "srgb");
        return p3.PngsaveBuffer(keep: Enums.ForeignKeep.Icc);
    }

    private static void AssertGreen(Image result)
    {
        Assert.InRange(result[0].Avg(), 30, 52);
        Assert.InRange(result[1].Avg(), 190, 210);
    }

    private static Image Noise() => Image.Gaussnoise(128, 128, sigma: 30, mean: 128).Cast(Enums.BandFormat.Uchar);

    private static (int Width, int Height) PngDimensions(ReadOnlySpan<byte> png) =>
        (BinaryPrimitives.ReadInt32BigEndian(png[16..]), BinaryPrimitives.ReadInt32BigEndian(png[20..]));
}
