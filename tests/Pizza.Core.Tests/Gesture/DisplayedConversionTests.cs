using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

public class DisplayedConversionTests
{
    [Fact]
    public void Resultado_da_conversao_na_tela_e_aceito_uma_vez()
    {
        var displayed = new DisplayedConversion();
        var id = displayed.Begin();

        Assert.True(displayed.TryFinish(id));
        Assert.False(displayed.TryFinish(id));
    }

    [Fact]
    public void Resultado_antigo_nao_ocupa_o_lugar_da_conversao_atual()
    {
        var displayed = new DisplayedConversion();
        var first = displayed.Begin();
        var second = displayed.Begin();

        Assert.False(displayed.TryFinish(first));
        Assert.True(displayed.TryFinish(second));
    }

    [Fact]
    public void Novo_gesto_descarta_o_resultado_pendente()
    {
        var displayed = new DisplayedConversion();
        var id = displayed.Begin();

        displayed.Clear();

        Assert.False(displayed.TryFinish(id));
    }
}
