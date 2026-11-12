using Pizza.Core.Visual;

namespace Pizza.Core.Tests.Visual;

/// <summary>Desfoque do vidro (ADR-023): três passadas de média, que se aproximam de um gaussiano, e saturação.</summary>
public class GlassBlurTests
{
    private static byte[] Solid(int width, int height, byte b, byte g, byte r)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = 7;
        }

        return pixels;
    }

    [Fact]
    public void Cor_uniforme_continua_igual_e_fica_opaca()
    {
        var pixels = Solid(40, 30, 50, 100, 150);

        GlassBlur.Apply(pixels, 40, 30, radius: 5, saturation: 1);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            Assert.Equal([50, 100, 150, 255], pixels[i..(i + 4)]);
        }
    }

    [Fact]
    public void Ponto_claro_se_espalha_e_perde_intensidade()
    {
        var pixels = Solid(41, 41, 0, 0, 0);
        var center = ((20 * 41) + 20) * 4;
        pixels[center] = pixels[center + 1] = pixels[center + 2] = 255;

        GlassBlur.Apply(pixels, 41, 41, radius: 3, saturation: 1);

        Assert.InRange(pixels[center], 1, 40);
        Assert.True(pixels[center + (4 * 4)] > 0);
        Assert.Equal(0, pixels[0]);
    }

    [Fact]
    public void Saturacao_nao_tinge_o_cinza()
    {
        var pixels = Solid(8, 8, 120, 120, 120);

        GlassBlur.Apply(pixels, 8, 8, radius: 1, saturation: 1.8);

        Assert.Equal([120, 120, 120, 255], pixels[..4]);
    }

    [Fact]
    public void Saturacao_afasta_a_cor_do_cinza()
    {
        var pixels = Solid(8, 8, 100, 100, 160);

        GlassBlur.Apply(pixels, 8, 8, radius: 1, saturation: 1.8);

        Assert.True(pixels[2] > 160);
        Assert.True(pixels[0] < 100);
    }

    [Fact]
    public void Meia_resolucao_fica_igual_a_olho_ao_desfoque_completo()
    {
        // NFR-03: o desfoque em meia resolução custa ~4× menos e o vidro continua o mesmo (diferença média < 3 de 255).
        const int width = 400, height = 562;
        var random = new Random(7);
        var screen = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = ((y * width) + x) * 4;
                var block = ((x / 37) + (y / 23)) % 2 == 0 ? 230 : 40; // janelas e texto, como uma tela de verdade
                screen[i] = (byte)Math.Clamp(block + random.Next(-25, 26), 0, 255);
                screen[i + 1] = (byte)Math.Clamp((x * 255 / width) + random.Next(-25, 26), 0, 255);
                screen[i + 2] = (byte)Math.Clamp((y * 255 / height) + random.Next(-25, 26), 0, 255);
                screen[i + 3] = 255;
            }
        }

        var full = screen.ToArray();
        var fast = screen.ToArray();
        GlassBlur.ApplyFullResolution(full, width, height, radius: 11, saturation: 1.8);
        GlassBlur.Apply(fast, width, height, radius: 11, saturation: 1.8);

        long total = 0;
        var worst = 0;
        for (var i = 0; i < full.Length; i++)
        {
            var difference = Math.Abs(full[i] - fast[i]);
            total += difference;
            worst = Math.Max(worst, difference);
        }

        Assert.True(total / (double)full.Length < 3, $"média {total / (double)full.Length:0.00}");
        Assert.True(worst < 40, $"pior {worst}");
        Assert.All(Enumerable.Range(0, width * height), p => Assert.Equal(255, fast[(p * 4) + 3]));
    }

    [Theory]
    [InlineData(11)] // meia resolução
    [InlineData(3)] // resolução cheia
    public void Desfoque_nao_deixa_pixels_da_tela_no_heap_gerenciado(int radius)
    {
        // ADR-023 e NFR-01: os rascunhos do desfoque guardam pixels da tela. Ficam fora do heap gerenciado, zerados
        // e soltos ao fim, para não esperar o coletor nem engordar a memória retida depois do gesto.
        var pixels = Solid(400, 562, 10, 20, 30);
        GlassBlur.Apply(pixels, 400, 562, radius, saturation: 1.8);

        var before = GC.GetAllocatedBytesForCurrentThread();
        GlassBlur.Apply(pixels, 400, 562, radius, saturation: 1.8);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 1024, $"alocou {allocated} bytes");
    }

    [Fact]
    public void Imagem_menor_que_o_raio_nao_quebra()
    {
        var pixels = Solid(2, 3, 10, 20, 30);

        GlassBlur.Apply(pixels, 2, 3, radius: 11, saturation: 1.8);

        Assert.Equal(255, pixels[3]);
    }
}
