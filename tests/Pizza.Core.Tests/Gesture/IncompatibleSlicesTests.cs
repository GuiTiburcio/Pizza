using Pizza.Core.Conversions;
using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

/// <summary>
/// ADR-036: na aba do arquivo, só as fatias que servem a ele; nas outras, as fatias aparecem apagadas e
/// apontam para a aba que converte o arquivo. No Brotinho, as fatias do motor de documentos não aparecem.
/// </summary>
[Collection(DocumentEngineSwitch.Name)]
public class IncompatibleSlicesTests
{
    [Fact]
    public void Na_aba_do_arquivo_so_aparecem_as_fatias_que_servem_a_ele()
    {
        using var _ = DocumentEngineSwitch.Set(true);

        var docs = RadialMenu.Slices(ConverterCatalog.DocsTab, "contrato.docx");

        Assert.Equal(["document.pdf", "document.txt"], docs.Select(slice => slice.Id));
        Assert.All(docs, slice => Assert.True(RadialMenu.Fits(slice, "contrato.docx")));
    }

    [Theory]
    [InlineData("foto.jpg", ConverterCatalog.DocsTab)]
    [InlineData("foto.jpg", ConverterCatalog.PdfTab)]
    [InlineData("foto.jpg", ConverterCatalog.AudioTab)]
    [InlineData("escritura.pdf", ConverterCatalog.ImageTab)]
    [InlineData("contrato.docx", ConverterCatalog.ImageTab)]
    public void Nas_outras_abas_as_fatias_aparecem_mas_nao_servem(string file, int tab)
    {
        using var _ = DocumentEngineSwitch.Set(true);

        var slices = RadialMenu.Pages(file).Where(page => page.Tab == tab)
            .SelectMany(page => RadialMenu.Slices(tab, file, page.Turn)).ToArray();

        Assert.Equal(ConverterCatalog.All.Count(converter => converter.Tab == tab), slices.Length);
        Assert.All(slices, slice => Assert.False(RadialMenu.Fits(slice, file)));
        Assert.All(slices, slice => Assert.False(slice.Accepts([file])));
    }

    [Theory]
    [InlineData("foto.jpg", ConverterCatalog.ImageTab)]
    [InlineData("escritura.pdf", ConverterCatalog.PdfTab)]
    [InlineData("contrato.docx", ConverterCatalog.DocsTab)]
    [InlineData("musica.mp3", ConverterCatalog.AudioTab)]
    public void O_aviso_aponta_para_a_aba_que_converte_o_arquivo(string file, int tab)
    {
        using var _ = DocumentEngineSwitch.Set(true);

        Assert.Equal(tab, RadialMenu.HomeTab(file));
        Assert.Equal($"Experimente a aba {MenuTabs.ShortNames[tab]}", RadialMenu.Hint(file));
    }

    [Theory]
    [InlineData("foto.jpg")]
    [InlineData("escritura.pdf")]
    [InlineData("contrato.docx")]
    [InlineData("planilha.xlsx")]
    public void No_Brotinho_as_fatias_do_motor_nao_aparecem_em_aba_nenhuma(string file)
    {
        using var _ = DocumentEngineSwitch.Set(false);

        var all = RadialMenu.Pages(file).SelectMany(page => RadialMenu.Slices(page.Tab, file, page.Turn));

        Assert.DoesNotContain(all, slice => slice.DocumentEngine);
    }

    [Theory]
    [InlineData("foto.jpg", true)]
    [InlineData("apresentacao.pptx", false)]
    public void O_menu_so_abre_com_alguma_fatia_que_sirva(string file, bool opens)
    {
        // FR-09: as fatias apagadas de outras abas não contam como conversão disponível.
        using var _ = DocumentEngineSwitch.Set(false);

        Assert.Equal(opens, RadialMenu.CanConvert(file));
    }

    [Fact]
    public void O_menu_abre_na_aba_do_arquivo_mesmo_com_as_outras_cheias()
    {
        using var _ = DocumentEngineSwitch.Set(true);

        Assert.Equal(ConverterCatalog.PdfTab, RadialMenu.Pages("escritura.pdf")[RadialMenu.FirstPage("escritura.pdf")].Tab);
        Assert.Equal(ConverterCatalog.AudioTab, RadialMenu.Pages("musica.mp3")[RadialMenu.FirstPage("musica.mp3")].Tab);
    }
}
