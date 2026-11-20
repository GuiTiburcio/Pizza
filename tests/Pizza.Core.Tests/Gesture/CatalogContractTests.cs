using Pizza.Core.Conversions;
using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

/// <summary>
/// Contrato do catálogo (plano de formatos, seção 5): toda fatia tem aba válida e ao menos uma entrada, e sem o
/// motor de documentos (Pizza Brotinho, ADR-034 e ADR-039) nenhuma fatia dele aparece, seja qual for o arquivo.
/// O arquivo-ouro por conversão fica para cada parcela (pendente de confirmação do mantenedor).
/// </summary>
[Collection(DocumentEngineSwitch.Name)]
public class CatalogContractTests
{
    private static readonly int[] Tabs =
        [ConverterCatalog.ImageTab, ConverterCatalog.PdfTab, ConverterCatalog.DocsTab, ConverterCatalog.AudioTab];

    public static TheoryData<string> Ids() => new(ConverterCatalog.All.Select(converter => converter.Id));

    [Theory]
    [MemberData(nameof(Ids))]
    public void Toda_fatia_tem_aba_valida_entradas_e_textos(string id)
    {
        var converter = ConverterCatalog.Find(id)!;

        Assert.Contains(converter.Tab, Tabs);
        Assert.NotEmpty(converter.InputExtensions);
        Assert.All(converter.InputExtensions, extension => Assert.Matches(@"^\.[a-z0-9]+$", extension));
        Assert.False(string.IsNullOrWhiteSpace(converter.Label));
        Assert.False(string.IsNullOrWhiteSpace(converter.Caption));
        Assert.Matches(@"^[a-z0-9]+$", converter.Extension);
    }

    [Fact]
    public void Sem_o_motor_nenhuma_fatia_dele_aparece_para_nenhuma_entrada_do_catalogo()
    {
        using var _ = DocumentEngineSwitch.Set(false);

        var files = ConverterCatalog.All.SelectMany(converter => converter.InputExtensions).Distinct()
            .Select(extension => "arquivo" + extension);

        foreach (var file in files)
        {
            var shown = RadialMenu.Pages(file).SelectMany(page => RadialMenu.Slices(page.Tab, file, page.Turn));
            Assert.DoesNotContain(shown, slice => slice.DocumentEngine);
        }
    }

    [Fact]
    public void Todo_arquivo_que_o_catalogo_aceita_tem_uma_aba_de_destino_com_o_motor()
    {
        using var _ = DocumentEngineSwitch.Set(true);

        foreach (var converter in ConverterCatalog.All.Where(converter => !converter.Merges))
        {
            var file = "arquivo" + converter.InputExtensions[0];
            Assert.True(RadialMenu.CanConvert(file), file);
            Assert.NotNull(RadialMenu.Hint(file));
        }
    }
}
