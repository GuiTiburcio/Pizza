using Pizza.Core.Visual;

namespace Pizza.Core.Tests.Visual;

/// <summary>ADR-037: a barra de abas segue o disco com atraso suave, sem passar do ponto.</summary>
public class BarFollowTests
{
    [Fact]
    public void Anda_parte_do_caminho_a_cada_quadro_e_nunca_passa()
    {
        var position = BarFollow.Step(0, 100, 10);

        Assert.InRange(position, 1, 99);
        Assert.True(BarFollow.Step(position, 100, 10) > position);
        Assert.True(BarFollow.Step(0, -100, 10) < 0);
    }

    [Fact]
    public void Chega_em_cerca_de_um_quarto_de_segundo()
    {
        double position = 0;
        for (var frame = 0; frame < 30; frame++)
        {
            position = BarFollow.Step(position, 300, 10);
        }

        Assert.Equal(300, position);
    }

    [Fact]
    public void Perto_do_destino_encaixa()
    {
        Assert.Equal(50, BarFollow.Step(49.7, 50, 10));
    }
}
