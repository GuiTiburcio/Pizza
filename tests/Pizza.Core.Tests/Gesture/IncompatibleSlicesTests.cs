using Pizza.Core.Conversions;
using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

/// <summary>
/// Adendo do ADR-039: as quatro abas permanecem visíveis; sem conversão para o arquivo, orientam qual tipo
/// usar, sem fatias incompatíveis. No Brotinho, as fatias do motor de documentos não aparecem.
/// </summary>
[Collection(DocumentEngineSwitch.Name)]
public class IncompatibleSlicesTests
{
    [Fact]
    public void Na_aba_do_arquivo_so_aparecem_as_fatias_que_servem_a_ele()
    {
        using var _ = DocumentEngineSwitch.Set(true);

        var docs = RadialMenu.Slices(ConverterCatalog.DocsTab, "contrato.docx");

        // A lista cresce com as etapas do Docs; o que importa é que nada de outro tipo apareça.
        Assert.Contains(docs, slice => slice.Id == "document.pdf");
        Assert.DoesNotContain(docs, slice => slice.Id == "document.csv");
        Assert.All(docs, slice => Assert.True(RadialMenu.Fits(slice, "contrato.docx")));
    }

    [Theory]
    [InlineData("foto.jpg", ConverterCatalog.DocsTab)]
    [InlineData("foto.jpg", ConverterCatalog.PdfTab)]
    [InlineData("foto.jpg", ConverterCatalog.AudioTab)]
    [InlineData("escritura.pdf", ConverterCatalog.ImageTab)]
    [InlineData("contrato.docx", ConverterCatalog.ImageTab)]
    public void Nas_outras_abas_nao_ha_fatias_e_a_orientacao_nao_promete_recurso_futuro(string file, int tab)
    {
        using var _ = DocumentEngineSwitch.Set(true);

        var slices = RadialMenu.Pages(file).Where(page => page.Tab == tab)
            .SelectMany(page => RadialMenu.Slices(tab, file, page.Turn)).ToArray();

        Assert.Empty(slices);
        Assert.Contains(RadialMenu.Pages(file), page => page.Tab == tab);
        Assert.StartsWith("Use ", RadialMenu.EmptyMessage(tab));
        Assert.NotEqual("Em breve", RadialMenu.EmptyMessage(tab));
    }

    [Theory]
    [InlineData(ConverterCatalog.ImageTab, "Use uma imagem")]
    [InlineData(ConverterCatalog.PdfTab, "Use um arquivo PDF")]
    [InlineData(ConverterCatalog.DocsTab, "Use um documento")]
    [InlineData(ConverterCatalog.AudioTab, "Use um arquivo de áudio")]
    public void Cada_aba_orienta_o_tipo_de_entrada(int tab, string expected)
    {
        Assert.Equal(expected, RadialMenu.EmptyMessage(tab));
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
        // FR-09: manter abas com orientações não torna um arquivo incompatível conversível.
        using var _ = DocumentEngineSwitch.Set(false);

        Assert.Equal(opens, RadialMenu.CanConvert(file));
    }

    [Fact]
    public void O_menu_abre_na_aba_do_arquivo_e_mantem_as_quatro_abas()
    {
        using var _ = DocumentEngineSwitch.Set(true);

        Assert.Equal(ConverterCatalog.PdfTab, RadialMenu.Pages("escritura.pdf")[RadialMenu.FirstPage("escritura.pdf")].Tab);
        Assert.Equal(ConverterCatalog.AudioTab, RadialMenu.Pages("musica.mp3")[RadialMenu.FirstPage("musica.mp3")].Tab);
        Assert.Equal(Enumerable.Range(0, MenuTabs.Names.Count), RadialMenu.Pages("escritura.pdf").Select(page => page.Tab).Distinct());
    }
}
