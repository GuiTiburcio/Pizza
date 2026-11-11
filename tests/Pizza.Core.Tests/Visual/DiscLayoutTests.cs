using Pizza.Core.Gesture;
using Pizza.Core.Visual;

namespace Pizza.Core.Tests.Visual;

/// <summary>Medidas do WF-1 e do WF-2 no desenho do menu.</summary>
public class DiscLayoutTests
{
    [Fact]
    public void Disco_de_380_px_cabe_na_tela_com_a_barra_acima_e_a_dica_abaixo()
    {
        Assert.Equal(DiscLayout.StripTop - DiscLayout.TabBarHeight, DiscLayout.BarGap);
        Assert.Equal(DiscLayout.DiscTop - (DiscLayout.StripTop + DiscLayout.StripHeight), DiscLayout.BarGap);
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
    public void Barra_B_tem_44_px_com_alvos_de_pelo_menos_44_px()
    {
        // ADR-026: 81% dos 54 px de antes; os alvos não ficam abaixo do mínimo do NFR-06.
        Assert.Equal(44, DiscLayout.TabBarHeight);
        Assert.True(DiscLayout.TabWidth >= 44);
        Assert.True(DiscLayout.TabIconTop >= DiscLayout.TabPadding);
        Assert.True(DiscLayout.TabLabelTop >= DiscLayout.TabIconTop + DiscLayout.TabIconSize);
        Assert.True(DiscLayout.TabLabelTop + DiscLayout.TabLabelHeight <= DiscLayout.TabBarHeight - DiscLayout.TabPadding);
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
    [InlineData(3)]
    [InlineData(4)]
    public void Niveis_ficam_centrados_dentro_da_janela_sem_se_sobrepor(int count)
    {
        // ADR-027: até quatro níveis de 84 px, centrados acima do disco.
        Assert.Equal(DiscLayout.Width - DiscLayout.LevelSpan(count - 1, count).Right, DiscLayout.LevelSpan(0, count).Left, 3);
        for (var level = 1; level < count; level++)
        {
            Assert.True(DiscLayout.LevelSpan(level, count).Left > DiscLayout.LevelSpan(level - 1, count).Right);
        }

        Assert.True(DiscLayout.LevelTop + DiscLayout.LevelHeight <= DiscLayout.DiscTop);
    }

    [Fact]
    public void Nivel_sob_o_cursor()
    {
        var (left, right) = DiscLayout.LevelSpan(2, 4);
        var y = DiscLayout.LevelTop + (DiscLayout.LevelHeight / 2.0);

        Assert.Equal(2, DiscLayout.LevelAt((left + right) / 2, y, 4));
        Assert.Null(DiscLayout.LevelAt((left + right) / 2, DiscLayout.DiscTop + 5, 4));
        Assert.Null(DiscLayout.LevelAt(2, y, 4));
        Assert.Null(DiscLayout.LevelAt((left + right) / 2, y, 0));
    }

    [Fact]
    public void Roda_troca_o_nivel_so_na_faixa()
    {
        Assert.True(DiscLayout.InStrip(DiscLayout.CenterX, DiscLayout.StripTop + 1));
        Assert.False(DiscLayout.InStrip(DiscLayout.CenterX, DiscLayout.CenterY));
        Assert.False(DiscLayout.InStrip(DiscLayout.CenterX, DiscLayout.TabBarHeight / 2.0));
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
