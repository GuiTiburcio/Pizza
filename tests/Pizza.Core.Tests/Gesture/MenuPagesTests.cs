using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

/// <summary>ADR-024 / TST-01: todos os formatos aparecem em voltas de até seis fatias.</summary>
public class MenuPagesTests
{
    [Fact]
    public void Jpg_tem_duas_voltas_antes_da_aba_pdf()
    {
        Assert.Equal([new MenuPage(0, 0), new MenuPage(0, 1), new MenuPage(1, 0), new MenuPage(2, 0), new MenuPage(3, 0)], RadialMenu.Pages("a.jpg"));
        Assert.Equal("GIF", Assert.Single(RadialMenu.Slices(0, "a.jpg", 1)).Label);
    }

    [Theory]
    [InlineData("a.jpg", 0, 0)]
    [InlineData("escritura.PDF", 1, 1)]
    public void Menu_abre_na_aba_do_tipo_do_arquivo(string path, int page, int tab)
    {
        // FR-03: o PDF não tem conversão de imagem, então a aba Imagem tem uma página só e a PDF é a segunda.
        Assert.Equal(page, RadialMenu.FirstPage(path));
        Assert.Equal(tab, RadialMenu.Pages(path)[page].Tab);
    }

    [Fact]
    public void Pdf_tem_juntar_dividir_e_girar_na_aba_pdf()
    {
        // Ordem da prancha 6A-1 (Comprimir, Juntar, Dividir, Imagens, Word, Girar), só com as que já existem.
        Assert.Equal(["Juntar", "Dividir", "Girar"], RadialMenu.Slices(1, "escritura.pdf").Select(slice => slice.Label));
        Assert.Empty(RadialMenu.Slices(0, "escritura.pdf"));
    }

    [Theory]
    [InlineData("a.jpg", 7)]
    [InlineData("a.avif", 7)]
    [InlineData("a.tiff", 7)]
    public void Nenhum_formato_da_aba_fica_de_fora(string path, int count)
    {
        var slices = RadialMenu.Pages(path).Where(page => page.Tab == 0).SelectMany(page => RadialMenu.Slices(page.Tab, path, page.Turn)).ToArray();
        Assert.Equal(count, slices.Length);
        Assert.Equal(count, slices.Select(slice => slice.Id).Distinct().Count());
    }

    [Fact]
    public void Fatia_que_ainda_encolhe_continua_com_alvo()
    {
        Assert.Equal(0, RadialMenu.SlotAt(0, -173, 0, [0.5, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void A_area_antiga_do_hover_e_tolerancia_para_evitar_oscilacao()
    {
        // A tolerância mantém o destaque estável perto da borda interna durante o crescimento.
        Assert.Equal(0, RadialMenu.SlotAt(0, -94, 0, [1, 0, 0, 0, 0, 0]));
    }
}
