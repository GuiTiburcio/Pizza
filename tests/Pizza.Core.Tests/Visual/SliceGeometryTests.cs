using Pizza.Core.Gesture;
using Pizza.Core.Visual;

namespace Pizza.Core.Tests.Visual;

/// <summary>WF-1 e WF-2: fatia de r 92 a r 168, 55°, cantos de 9 px; no hover, 106% e 5 px para fora.</summary>
public class SliceGeometryTests
{
    private const double Center = 200;

    private static double Distance((double X, double Y) point, double cx, double cy) =>
        Math.Sqrt(((point.X - cx) * (point.X - cx)) + ((point.Y - cy) * (point.Y - cy)));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void Em_repouso_fica_entre_os_raios(int slot)
    {
        var shape = SliceGeometry.Build(RadialMenu.SlotAngle(slot), 0, Center, Center);

        foreach (var point in shape.AllPoints())
        {
            Assert.InRange(Distance(point, Center, Center), RadialMenu.InnerRadius - 0.01, RadialMenu.OuterRadius + 0.01);
        }
    }

    [Fact]
    public void Cantos_arredondados_encurtam_os_arcos()
    {
        var shape = SliceGeometry.Build(-90, 0, Center, Center);

        var outerCorner = 9 / RadialMenu.OuterRadius * 180 / Math.PI;
        Assert.Equal(55 - (2 * outerCorner), shape.OuterArc.Sweep, 3);
        Assert.True(Math.Abs(shape.InnerArc.Sweep) < shape.OuterArc.Sweep);
    }

    [Fact]
    public void Fatia_e_simetrica_ao_eixo_dela()
    {
        var shape = SliceGeometry.Build(-90, 0, Center, Center);

        var xs = shape.AllPoints().Select(point => Math.Round(point.X - Center, 6)).Order().ToArray();
        Assert.Equal(xs, xs.Select(x => -x).Order().ToArray());
    }

    [Fact]
    public void Hover_cresce_6_por_cento_e_sai_5_px_na_direcao_da_fatia()
    {
        var rest = SliceGeometry.Build(-90, 0, Center, Center);
        var hover = SliceGeometry.Build(-90, 1, Center, Center);

        Assert.Equal(rest.OuterArc.Radius * 1.06, hover.OuterArc.Radius, 6);
        Assert.Equal(Center, hover.OuterArc.CenterX, 6);
        Assert.Equal(Center - 5, hover.OuterArc.CenterY, 6);
    }

    [Fact]
    public void Meio_do_hover_fica_no_meio_do_caminho()
    {
        var half = SliceGeometry.Build(0, 0.5, Center, Center);

        Assert.Equal(Center + 2.5, half.OuterArc.CenterX, 6);
        Assert.Equal(RadialMenu.OuterRadius * 1.03, half.OuterArc.Radius, 6);
    }
}
