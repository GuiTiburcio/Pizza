using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

public class MenuTabsTests
{
    [Fact]
    public void Abas_na_ordem_da_spec()
    {
        Assert.Equal(["Imagem", "PDF", "Documento", "Áudio"], MenuTabs.Names);
    }
}
