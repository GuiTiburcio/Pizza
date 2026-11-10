using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

public class RadialMenuTests
{
    [Theory]
    [InlineData(0, -130, 0)] // topo
    [InlineData(113, -65, 1)]
    [InlineData(113, 65, 2)]
    [InlineData(0, 130, 3)]
    [InlineData(-113, 65, 4)]
    [InlineData(-113, -65, 5)]
    public void Centro_de_cada_fatia_acerta_a_fatia(double x, double y, int expected)
    {
        // FR-05: por ângulo e raio.
        Assert.Equal(expected, RadialMenu.SlotAt(x, y));
    }

    [Theory]
    [InlineData(0, 0)] // miolo
    [InlineData(0, -91)] // antes da borda interna
    [InlineData(0, -169)] // depois da borda externa
    [InlineData(65, -113)] // na folga entre duas fatias, a -60°
    public void Fora_das_fatias_nao_ha_alvo(double x, double y)
    {
        Assert.Null(RadialMenu.SlotAt(x, y));
    }

    [Fact]
    public void Alvo_acompanha_as_fatias_giradas_na_troca_de_aba()
    {
        // Fatia 0 desenhada a -90° + 40°: o cursor ali está sobre ela, não sobre a vizinha.
        var angle = (-90 + 40) * Math.PI / 180;
        var x = 130 * Math.Cos(angle);
        var y = 130 * Math.Sin(angle);

        Assert.Equal(1, RadialMenu.SlotAt(x, y));
        Assert.Equal(0, RadialMenu.SlotAt(x, y, rotationDegrees: 40, hoveredSlot: null, hover: 0));
    }

    [Fact]
    public void Fatia_em_hover_aceita_o_drop_na_parte_que_cresceu()
    {
        // No hover, a fatia 0 vai até cerca de r 183 (106% e 5 px para fora).
        Assert.Null(RadialMenu.SlotAt(0, -178));
        Assert.Equal(0, RadialMenu.SlotAt(0, -178, rotationDegrees: 0, hoveredSlot: 0, hover: 1));
    }

    [Fact]
    public void Crescimento_do_hover_nao_rouba_o_alvo_das_outras_fatias()
    {
        Assert.Equal(3, RadialMenu.SlotAt(0, 130, rotationDegrees: 0, hoveredSlot: 0, hover: 1));
    }

    [Theory]
    [InlineData(0, -92, 0)]
    [InlineData(0, -168, 0)]
    public void Bordas_da_fatia_contam(double x, double y, int expected)
    {
        Assert.Equal(expected, RadialMenu.SlotAt(x, y));
    }

    [Theory]
    [InlineData("C:\\a\\fachada.jpg", "PNG,WEBP,PDF,TIFF,ICO,GIF")]
    [InlineData("C:\\a\\FACHADA.JPEG", "PNG,WEBP,PDF,TIFF,ICO,GIF")]
    [InlineData("a.png", "JPG,WEBP,PDF,TIFF,ICO,GIF")]
    [InlineData("a.webp", "JPG,PNG,PDF,TIFF,ICO,GIF")]
    [InlineData("a.tif", "JPG,PNG,WEBP,PDF,ICO,GIF")]
    [InlineData("a.tiff", "JPG,PNG,WEBP,PDF,ICO,GIF")]
    [InlineData("a.gif", "JPG,PNG,WEBP,PDF,TIFF,ICO")]
    [InlineData("a.avif", "JPG,PNG,WEBP,PDF,TIFF,ICO")] // Mais de 6: o último da ordem fica de fora.
    public void Fatias_seguem_a_ordem_sem_o_formato_de_origem(string path, string expected)
    {
        // ADR-012.
        Assert.Equal(expected, string.Join(',', RadialMenu.Slices(0, path).Select(slice => slice.Label)));
    }

    [Fact]
    public void Fatia_png_dispara_a_conversao_png()
    {
        var slice = RadialMenu.Slices(0, "a.jpg")[0];

        Assert.Equal("image.png", slice.Id);
        Assert.Equal("png", slice.Extension);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Abas_ainda_sem_conversao_ficam_vazias(int tab)
    {
        Assert.Empty(RadialMenu.Slices(tab, "a.jpg"));
    }

    [Theory]
    [InlineData("a.jpg", true)]
    [InlineData("a.JPEG", true)]
    [InlineData("a.png", true)]
    [InlineData("a.avif", true)]
    [InlineData("a.bmp", false)]
    [InlineData("a.heic", false)]
    [InlineData("a.txt", false)]
    [InlineData("a.exe", false)]
    [InlineData("sem-extensao", false)]
    public void So_abre_para_arquivo_com_conversao(string path, bool expected)
    {
        // FR-09.
        Assert.Equal(expected, RadialMenu.CanConvert(path));
    }
}
