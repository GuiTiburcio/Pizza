using Pizza.Core.Notifications;

namespace Pizza.Core.Tests.Notifications;

/// <summary>Movimento dos cartões: receita de toast da skill animate, 400 ms com a curva "ease".</summary>
public class CardMotionTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.8024)]
    [InlineData(1, 1)]
    public void Curva_ease_bate_com_a_do_css(double time, double expected)
    {
        Assert.Equal(expected, CardTransition.Ease(time), 3);
    }

    [Fact]
    public void Chega_ao_destino_em_400_ms()
    {
        var transition = new CardTransition(100);
        transition.Retarget(0);

        transition.Advance(399);
        Assert.False(transition.IsDone);

        transition.Advance(1);
        Assert.True(transition.IsDone);
        Assert.Equal(0, transition.Value);
    }

    [Fact]
    public void Mudar_o_destino_no_meio_continua_de_onde_esta()
    {
        // Pilha reorganizada durante a animação: sem salto.
        var transition = new CardTransition(100);
        transition.Retarget(0);
        transition.Advance(100);
        var current = transition.Value;

        transition.Retarget(200);

        Assert.Equal(current, transition.Value);
        Assert.False(transition.IsDone);
    }

    [Fact]
    public void Mesmo_destino_nao_reinicia()
    {
        var transition = new CardTransition(100);
        transition.Retarget(0);
        transition.Advance(200);
        var current = transition.Value;

        transition.Retarget(0);

        Assert.Equal(current, transition.Value);
    }

    [Fact]
    public void Duracao_pode_ser_a_do_hover_da_fatia()
    {
        // WF-2: 180 ms, curva "ease".
        var transition = new CardTransition(0, 180);
        transition.Retarget(1);

        transition.Advance(179);
        Assert.False(transition.IsDone);

        transition.Advance(1);
        Assert.Equal(1, transition.Value);
    }

    [Fact]
    public void Snap_vai_direto()
    {
        var transition = new CardTransition(100);
        transition.Retarget(0);

        transition.Snap(50);

        Assert.True(transition.IsDone);
        Assert.Equal(50, transition.Value);
    }
}
