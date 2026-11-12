using System.Buffers.Binary;
using System.Text;
using NetVips;
using Pizza.Converters;
using Pizza.Core.Conversions;
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

    [Theory]
    [InlineData("svgload_source", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"/>")]
    [InlineData("matrixload_source", "1 1\n0\n")]
    public void Decodificador_que_existe_no_build_mas_fora_da_lista_e_bloqueado(string loader, string content)
    {
        // V-04 e SEC-45: o decodificador está no libvips empacotado; a recusa prova o bloqueio, não a falta dele.
        Assert.NotEqual(0, (long)NetVips.NetVips.TypeFind("VipsForeignLoad", loader));

        var (error, _) = Convert(Encoding.UTF8.GetBytes(content));

        Assert.Equal("UNSUPPORTED", error);
    }

    [Fact]
    public void Vale_o_conteudo_dentro_da_aba_imagem()
    {
        // SEC-45: o formato real precisa estar na lista e corresponder à aba, mesmo com outra extensão.
        using var image = Solid(8, 8);

        var (error, png) = Convert(image.PngsaveBuffer());

        Assert.Null(error);
        Assert.True(IsPng(png));
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

    [Theory]
    [InlineData("75", 48, 36)]
    [InlineData("50", 32, 24)]
    public void Resolucao_reduz_as_dimensoes(string resolution, int width, int height)
    {
        var (error, png) = Convert(Jpeg(64, 48), ("resolution", resolution));

        Assert.Null(error);
        Assert.Equal((width, height), Dimensions(png));
    }

    [Fact]
    public void Resolucao_vale_tambem_para_foto_girada()
    {
        using var image = Solid(40, 20).Mutate(m => m.Set(GValue.GIntType, "orientation", 6));

        var (_, png) = Convert(image.JpegsaveBuffer(keep: Enums.ForeignKeep.All), ("resolution", "50"));

        Assert.Equal((10, 20), Dimensions(png));
    }

    [Theory]
    [InlineData("fast")]
    [InlineData("balanced")]
    [InlineData("maximum")]
    public void Compressao_nao_muda_os_pixels(string compression)
    {
        var (error, png) = Convert(Jpeg(32, 24, [200, 40, 90]), ("compression", compression));

        Assert.Null(error);
        using var result = Image.NewFromBuffer(png);
        Assert.InRange(result[0].Avg(), 194, 206);
        Assert.Equal((32, 24), Dimensions(png));
    }

    [Fact]
    public void Compressao_maxima_nao_gera_arquivo_maior_que_a_rapida()
    {
        using var noise = Image.Gaussnoise(128, 128, sigma: 20, mean: 128).Cast(Enums.BandFormat.Uchar);
        var jpeg = noise.JpegsaveBuffer(q: 90);

        var (_, fast) = Convert(jpeg, ("compression", "fast"));
        var (_, maximum) = Convert(jpeg, ("compression", "maximum"));

        Assert.True(maximum.Length <= fast.Length);
    }

    [Fact]
    public void Informa_o_andamento_da_conversao()
    {
        // ADR-029: o aviso mostra a porcentagem real; precisa chegar ao menos um valor, de 0 a 100.
        using var noise = Image.Gaussnoise(2400, 1800).Cast(Enums.BandFormat.Uchar);
        var jpeg = noise.JpegsaveBuffer(q: 90);
        var reported = new Collected();

        using var input = new MemoryStream(jpeg);
        using var output = new MemoryStream();
        var error = new ImagePngConverter().Convert(input, output, ConverterCatalog.ResolveOptions(ConverterCatalog.Find(ImagePngConverter.ConverterId)!, new Dictionary<string, string>())!, reported);

        Assert.Null(error);
        Assert.NotEmpty(reported.Values);
        Assert.All(reported.Values, percent => Assert.InRange(percent, 0, 100));
    }

    private sealed class Collected : IProgress<int>
    {
        public List<int> Values { get; } = [];

        public void Report(int value)
        {
            lock (Values)
            {
                Values.Add(value);
            }
        }
    }

    private static (string? Error, byte[] Png) Convert(byte[] source, params (string Key, string Value)[] options)
    {
        var given = options.ToDictionary(o => o.Key, o => o.Value);
        using var input = new MemoryStream(source);
        using var output = new MemoryStream();
        var error = new ImagePngConverter().Convert(input, output, ConverterCatalog.ResolveOptions(ConverterCatalog.Find(ImagePngConverter.ConverterId)!, given)!);
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
