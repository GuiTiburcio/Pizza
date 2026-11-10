using Pizza.Core.Gesture;
using Pizza.Core.Visual;

namespace Pizza.Core.Tests.Visual;

/// <summary>Medidas do WF-1 e do WF-2 no desenho do menu.</summary>
public class DiscLayoutTests
{
    [Fact]
    public void Disco_de_380_px_cabe_na_tela_com_a_barra_acima_e_a_dica_abaixo()
    {
        Assert.Equal(DiscLayout.DiscTop - DiscLayout.TabBarHeight, DiscLayout.BarGap);
        Assert.True(DiscLayout.CenterX - 190 >= 0 && DiscLayout.CenterX + 190 <= DiscLayout.Width);
        Assert.True(DiscLayout.HintTop >= DiscLayout.CenterY + 190);
        Assert.True(DiscLayout.HintTop + DiscLayout.HintHeight <= DiscLayout.Height);
    }

    [Fact]
    public void Fatia_em_hover_continua_dentro_da_janela()
    {
        var shape = SliceGeometry.Build(RadialMenu.SlotAngle(3), 1, DiscLayout.CenterX, DiscLayout.CenterY);

        foreach (var point in shape.AllPoints())
        {
            Assert.InRange(point.X, 0, DiscLayout.Width);
            Assert.InRange(point.Y, DiscLayout.TabBarHeight, DiscLayout.HintTop);
        }
    }

    [Fact]
    public void Abas_ficam_dentro_da_barra_sem_se_sobrepor()
    {
        for (var tab = 0; tab < MenuTabs.Names.Count; tab++)
        {
            var (left, right) = DiscLayout.TabSpan(tab);
            Assert.True(left >= DiscLayout.TabBarLeft && right <= DiscLayout.TabBarLeft + DiscLayout.TabBarWidth);
            if (tab > 0)
            {
                Assert.True(left >= DiscLayout.TabSpan(tab - 1).Right);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public void Destaque_parado_cobre_a_aba(int tab)
    {
        Assert.Equal(DiscLayout.TabSpan(tab).Left, DiscLayout.HighlightLeft(tab), 3);
    }

    [Fact]
    public void Destaque_desliza_entre_as_abas()
    {
        var middle = DiscLayout.HighlightLeft(1.5);

        Assert.Equal((DiscLayout.TabSpan(1).Left + DiscLayout.TabSpan(2).Left) / 2, middle, 3);
    }

    [Fact]
    public void Destaque_acompanha_a_volta_completa_da_roda()
    {
        Assert.Equal(DiscLayout.HighlightLeft(0), DiscLayout.HighlightLeft(4), 3);
        Assert.Equal(DiscLayout.HighlightLeft(3), DiscLayout.HighlightLeft(-1), 3);
    }
}
