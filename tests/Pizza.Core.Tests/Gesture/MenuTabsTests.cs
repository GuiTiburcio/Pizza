using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

public class MenuTabsTests
{
    [Fact]
    public void Abas_na_ordem_da_spec()
    {
        Assert.Equal(["Imagem", "PDF", "Documento", "Áudio"], MenuTabs.Names);
    }

    [Theory]
    [InlineData(0, -120, 1)]
    [InlineData(1, -120, 2)]
    [InlineData(3, -120, 0)]
    [InlineData(0, 120, 3)]
    [InlineData(2, 120, 1)]
    [InlineData(2, 0, 2)]
    public void Step_avanca_para_baixo_volta_para_cima_e_da_a_volta(int atual, int delta, int esperado)
    {
        Assert.Equal(esperado, MenuTabs.Step(atual, delta));
    }
}
