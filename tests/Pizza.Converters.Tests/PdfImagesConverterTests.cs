using NetVips;
using Pizza.Converters.Pdf;
using Pizza.Core.Output;

namespace Pizza.Converters.Tests;

/// <summary>Imagens (adendo da ADR-033, prancha 6B-1): o PDFium desenha cada página, e cada uma vira uma parte do pacote.</summary>
public sealed class PdfImagesConverterTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "pizza-imagens-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => File.Delete(_file);

    private (string? Error, List<byte[]> Parts) Render(byte[] pdf, string resolution = "150", string format = "jpg", string quality = "85")
    {
        using (var source = new MemoryStream(pdf))
        using (var destination = File.Create(_file))
        {
            var options = new Dictionary<string, string> { ["resolution"] = resolution, ["format"] = format, ["quality"] = quality };
            var error = new PdfImagesConverter().Convert(source, destination, options);
            if (error is not null)
            {
                return (error, []);
            }
        }

        using var handle = File.OpenHandle(_file);
        var bytes = File.ReadAllBytes(_file);
        return (null, OutputBundle.Read(handle, 2000, format).Select(part => bytes[(int)part.Offset..(int)(part.Offset + part.Length)]).ToList());
    }

    [Theory]
    [InlineData("96", 267, 400)]
    [InlineData("150", 417, 625)]
    [InlineData("300", 833, 1250)]
    public void Cada_pagina_vira_um_jpg_do_tamanho_da_resolucao(string resolution, int width, int height)
    {
        // 200 × 300 pt: 2,78 × 4,17 polegadas.
        var (error, parts) = Render(TestPdf.Drawn(3, "Pizza"), resolution);

        Assert.Null(error);
        Assert.Equal(3, parts.Count);
        Assert.All(parts, part =>
        {
            Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, part[..3]);
            using var image = Image.NewFromBuffer(part);
            Assert.Equal((width, height), (image.Width, image.Height));
        });
    }

    [Fact]
    public void Png_desenha_o_conteudo_da_pagina()
    {
        // Contraprova do desenho: a metade esquerda é preta e a direita, o fundo branco.
        var (error, parts) = Render(TestPdf.Drawn(1, blackLeftHalf: true), "96", "png");

        Assert.Null(error);
        var part = Assert.Single(parts);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, part[..4]);
        using var image = Image.NewFromBuffer(part);
        using var left = image.Crop(10, 10, 100, 380);
        using var right = image.Crop(160, 10, 100, 380);
        Assert.True(left.Avg() < 10, $"esquerda {left.Avg()}");
        Assert.True(right.Avg() > 245, $"direita {right.Avg()}");
    }

    [Fact]
    public void Qualidade_menor_faz_jpg_menor()
    {
        var pdf = TestPdf.Drawn(1, "Texto para comparar a qualidade do JPG", blackLeftHalf: true);

        var high = Assert.Single(Render(pdf, "150", "jpg", "95").Parts);
        var low = Assert.Single(Render(pdf, "150", "jpg", "60").Parts);

        Assert.True(low.Length < high.Length, $"60: {low.Length} bytes; 95: {high.Length} bytes");
    }

    [Fact]
    public void Pdf_com_senha_recebe_password_e_nao_grava_nada()
    {
        var (error, parts) = Render(TestPdf.Encrypt(TestPdf.Pages(2), "segredo"));

        Assert.Equal("PASSWORD", error);
        Assert.Empty(parts);
    }

    [Fact]
    public void Arquivo_que_nao_e_pdf_recebe_corrupted()
    {
        Assert.Equal("CORRUPTED", Render("isto não é um PDF"u8.ToArray()).Error);
    }

    [Fact]
    public void Pagina_que_passa_de_100_milhoes_de_pixels_recebe_limit_exceeded()
    {
        // SEC-41 e TST-08: 200 × 200 polegadas a 300 dpi seriam 3,6 bilhões de pixels.
        Assert.Equal("LIMIT_EXCEEDED", Render(TestPdf.Sized(14400, 14400), "300").Error);
    }
}
