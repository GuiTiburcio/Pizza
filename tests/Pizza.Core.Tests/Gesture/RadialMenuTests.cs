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

    [Theory]
    [InlineData(0, -92, 0)]
    [InlineData(0, -168, 0)]
    public void Bordas_da_fatia_contam(double x, double y, int expected)
    {
        Assert.Equal(expected, RadialMenu.SlotAt(x, y));
    }

    [Theory]
    [InlineData("C:\\a\\fachada.jpg")]
    [InlineData("C:\\a\\FACHADA.JPEG")]
    public void Jpg_tem_a_fatia_png_na_aba_imagem(string path)
    {
        var slice = Assert.Single(RadialMenu.Slices(0, path));

        Assert.Equal("PNG", slice.Label);
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
    [InlineData("a.txt", false)]
    [InlineData("a.exe", false)]
    [InlineData("sem-extensao", false)]
    public void So_abre_para_arquivo_com_conversao(string path, bool expected)
    {
        // FR-09.
        Assert.Equal(expected, RadialMenu.CanConvert(path));
    }
}
