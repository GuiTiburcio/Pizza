using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

public class MenuTabsTests
{
    [Fact]
    public void Abas_na_ordem_da_spec()
    {
        Assert.Equal(["Imagem", "PDF", "Documento", "Áudio"], MenuTabs.Names);
    }

    [Fact]
    public void Barra_B_mostra_o_nome_curto_de_cada_aba()
    {
        // ADR-026, prancha Ref-Barra-Abas: ícone com o nome curto embaixo; o nome completo fica em Names.
        Assert.Equal(["Imagem", "PDF", "Docs", "Áudio"], MenuTabs.ShortNames);
    }
}
