using Pizza.Core.Conversions;

namespace Pizza.Converters.Tests;

public class ConverterRegistryTests
{
    [Fact]
    public void Cada_item_do_catalogo_tem_um_conversor_e_vice_versa()
    {
        // O menu lê o catálogo e o Worker lê o registro: os dois precisam ser a mesma lista.
        Assert.Equal(
            ConverterCatalog.All.Select(c => c.Id).Order(),
            ConverterRegistry.All.Select(c => c.Id).Order());
    }

    [Fact]
    public void Conversor_desconhecido_nao_e_encontrado()
    {
        Assert.Null(ConverterRegistry.Find("image.bmp"));
    }
}
