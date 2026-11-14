using Pizza.Core.Visual;

namespace Pizza.Core.Tests.Visual;

/// <summary>ADR-037: menus com 1/15 da tela, fator do usuário de 50% a 100%, alças e preferências.</summary>
public class MenuScaleTests
{
    [Theory]
    [InlineData(1920, 1032)]
    [InlineData(3840, 2112)]
    [InlineData(1366, 728)]
    [InlineData(2560, 1392)]
    public void Padrao_ocupa_um_quinze_avos_da_area_util(int width, int height)
    {
        var scale = MenuScale.Fit(width, height, MenuScale.Max);
        var area = DiscLayout.Width * scale * DiscLayout.Height * scale;

        Assert.Equal(width * (double)height / 15, area, 1e-6 * width * height);
    }

    [Fact]
    public void Em_1080p_o_menu_fica_com_cerca_de_77_por_cento_do_tamanho_de_hoje()
    {
        Assert.InRange(MenuScale.Fit(1920, 1032, MenuScale.Max), 0.76, 0.78);
    }

    [Fact]
    public void A_escala_do_Windows_nao_muda_a_proporcao_na_tela()
    {
        // A conta só usa a área útil em pixels físicos: 4K a 150% ou a 200% dá o mesmo dpi efetivo.
        Assert.Equal((int)Math.Round(96 * MenuScale.Fit(3840, 2112, 1)), MenuScale.EffectiveDpi(3840, 2112, 1));
        Assert.InRange(MenuScale.EffectiveDpi(3840, 2112, 1) - (2 * MenuScale.EffectiveDpi(1920, 1056, 1)), -1, 1);
    }

    [Theory]
    [InlineData(0.2, 0.5)]
    [InlineData(0.5, 0.5)]
    [InlineData(0.8, 0.8)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.7, 1.0)]
    [InlineData(double.NaN, 1.0)]
    public void Fator_do_usuario_fica_entre_metade_e_o_teto(double user, double expected)
    {
        Assert.Equal(expected, MenuScale.Clamp(user));
    }

    [Fact]
    public void Nunca_passa_de_um_quinze_avos()
    {
        Assert.Equal(MenuScale.Fit(1920, 1032, 1), MenuScale.Fit(1920, 1032, 3));
    }

    [Theory]
    [InlineData(120, 0.85)]
    [InlineData(-120, 0.75)]
    [InlineData(240, 0.9)]
    [InlineData(-1200, 0.5)]
    [InlineData(1200, 1.0)]
    public void Ctrl_e_roda_anda_cinco_por_cento_por_entalhe(int delta, double expected)
    {
        Assert.Equal(expected, MenuScale.Step(0.8, delta), 9);
    }

    [Theory]
    [InlineData(190, 190, 0.8)]
    [InlineData(190, 95, 0.5)]
    [InlineData(190, 209, 0.88)]
    [InlineData(190, 400, 1.0)]
    [InlineData(0, 100, 0.8)]
    public void Puxar_a_borda_muda_o_fator_na_proporcao_da_distancia(double start, double now, double expected)
    {
        Assert.Equal(expected, MenuScale.Drag(0.8, start, now), 9);
    }
}
