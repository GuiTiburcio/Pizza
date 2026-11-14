using Pizza.Core.Visual;

namespace Pizza.Core.Tests.Visual;

/// <summary>ADR-037: onde o clique move, onde redimensiona e onde continua sendo alvo.</summary>
public class MenuGripTests
{
    [Theory]
    [InlineData(DiscLayout.CenterX + 190, DiscLayout.CenterY)]
    [InlineData(DiscLayout.CenterX, DiscLayout.CenterY + 186)]
    [InlineData(DiscLayout.CenterX - 195, DiscLayout.CenterY)]
    public void Borda_do_circulo_redimensiona(double x, double y)
    {
        Assert.Equal(GripZone.Resize, MenuGrip.Disc(x, y, onTarget: false));
    }

    [Fact]
    public void Borda_vale_mais_que_alvo_vizinho()
    {
        Assert.Equal(GripZone.Resize, MenuGrip.Disc(DiscLayout.CenterX + 188, DiscLayout.CenterY, onTarget: true));
    }

    [Fact]
    public void Miolo_e_vidro_fora_dos_alvos_movem()
    {
        Assert.Equal(GripZone.Move, MenuGrip.Disc(DiscLayout.CenterX, DiscLayout.CenterY, onTarget: false));
        Assert.Equal(GripZone.Move, MenuGrip.Disc(DiscLayout.CenterX, DiscLayout.HintTop + 4, onTarget: false));
    }

    [Fact]
    public void Fatia_continua_alvo()
    {
        Assert.Equal(GripZone.Target, MenuGrip.Disc(DiscLayout.CenterX + 100, DiscLayout.CenterY, onTarget: true));
    }

    [Fact]
    public void Barra_de_abas_move_e_nivel_continua_alvo()
    {
        Assert.Equal(GripZone.Move, MenuGrip.Bar(onLevel: false));
        Assert.Equal(GripZone.Target, MenuGrip.Bar(onLevel: true));
    }

    [Theory]
    [InlineData(100, 0, ResizeDirection.Horizontal)]
    [InlineData(-100, 5, ResizeDirection.Horizontal)]
    [InlineData(0, 100, ResizeDirection.Vertical)]
    [InlineData(3, -100, ResizeDirection.Vertical)]
    [InlineData(70, 70, ResizeDirection.Diagonal)]
    [InlineData(-70, -70, ResizeDirection.Diagonal)]
    [InlineData(70, -70, ResizeDirection.AntiDiagonal)]
    [InlineData(-70, 70, ResizeDirection.AntiDiagonal)]
    public void Seta_de_tamanho_aponta_para_fora_do_centro(double dx, double dy, ResizeDirection expected)
    {
        Assert.Equal(expected, MenuGrip.Direction(dx, dy));
    }

    [Theory]
    [InlineData(2, 100)]
    [InlineData(298, 100)]
    [InlineData(150, 2)]
    [InlineData(150, 398)]
    public void Bordas_do_retangulo_redimensionam(double x, double y)
    {
        Assert.Equal(GripZone.Resize, MenuGrip.Rectangle(x, y, 300, 400, onTarget: false));
    }

    [Fact]
    public void Dentro_do_retangulo_move_fora_dos_botoes()
    {
        Assert.Equal(GripZone.Move, MenuGrip.Rectangle(150, 200, 300, 400, onTarget: false));
        Assert.Equal(GripZone.Target, MenuGrip.Rectangle(150, 200, 300, 400, onTarget: true));
    }
}
