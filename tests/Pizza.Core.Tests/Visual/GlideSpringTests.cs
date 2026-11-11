using Pizza.Core.Visual;

namespace Pizza.Core.Tests.Visual;

/// <summary>TST-01 / ADR-027: a bolha de vidro desliza na mola do giro de aba e cresce no meio do caminho.</summary>
public class GlideSpringTests
{
    [Fact]
    public void Desliza_ate_o_destino_e_para()
    {
        var glide = new GlideSpring();
        glide.Retarget(2);

        Assert.True(glide.IsAnimating);
        for (var i = 0; i < 60; i++)
        {
            glide.Advance(10);
        }

        Assert.False(glide.IsAnimating);
        Assert.Equal(2, glide.Position);
        Assert.Equal(0, glide.Swell);
    }

    [Fact]
    public void Cresce_no_meio_do_caminho_e_nao_nas_pontas()
    {
        var glide = new GlideSpring();
        Assert.Equal(0, glide.Swell);

        glide.Retarget(1);
        glide.Advance(1);
        var start = glide.Swell;
        var peak = 0.0;
        while (glide.IsAnimating)
        {
            glide.Advance(5);
            peak = Math.Max(peak, glide.Swell);
        }

        Assert.True(start < 0.2);
        Assert.True(peak > 0.95);
    }

    [Fact]
    public void Na_mola_de_240_ms_quase_todo_o_caminho_sai_a_tempo()
    {
        var glide = new GlideSpring();
        glide.Retarget(1);
        glide.Advance(240);

        Assert.True(glide.Position > 0.97);
    }

    [Fact]
    public void Snap_vai_direto_sem_crescer()
    {
        var glide = new GlideSpring();
        glide.Snap(3);

        Assert.Equal(3, glide.Position);
        Assert.False(glide.IsAnimating);
        Assert.Equal(0, glide.Swell);
    }

    [Theory]
    [InlineData(5, 0, 6)]
    [InlineData(0, 5, -1)]
    [InlineData(1, 3, 3)]
    public void Nas_fatias_vai_pelo_caminho_mais_curto_do_circulo(int from, int to, double goal)
    {
        var glide = new GlideSpring();
        glide.Snap(from);
        glide.RetargetOnRing(to, 6);

        Assert.Equal(goal, glide.Goal);
    }
}
