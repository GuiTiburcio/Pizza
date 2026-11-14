using Pizza.Core.Visual;

namespace Pizza.Core.Tests.Visual;

/// <summary>ADR-037 e PRIV-15: só o fator e posições em fração; nada de arquivo, e lixo vira padrão.</summary>
public class MenuPrefsTests
{
    [Fact]
    public void Ida_e_volta_preserva_fator_e_posicoes()
    {
        var prefs = new MenuPrefs { Scale = 0.65 };
        prefs.Positions["options"] = (0.25, 0.75);

        var read = MenuPrefs.Parse(prefs.Serialize());

        Assert.Equal(0.65, read.Scale, 9);
        Assert.Equal((0.25, 0.75), read.Positions["options"]);
    }

    [Fact]
    public void Arquivo_vazio_ou_estranho_volta_ao_padrao()
    {
        Assert.Equal(MenuScale.Max, MenuPrefs.Parse("").Scale);
        var junk = MenuPrefs.Parse("scale=abc\nposition.x=1;2\nposition.y=-4;9\n\u0000\u0001");
        Assert.Equal(MenuScale.Max, junk.Scale);
        Assert.Equal((1.0, 1.0), junk.Positions["x"]);
        Assert.Equal((0.0, 1.0), junk.Positions["y"]);
        Assert.Equal(2, junk.Positions.Count);
    }

    [Fact]
    public void Fator_fora_da_faixa_e_limitado()
    {
        Assert.Equal(0.5, MenuPrefs.Parse("scale=0.1").Scale);
        Assert.Equal(1.0, MenuPrefs.Parse("scale=9").Scale);
    }

    [Fact]
    public void Texto_gravado_nao_tem_nada_alem_de_numeros_e_chaves_fixas()
    {
        var prefs = new MenuPrefs { Scale = 0.9 };
        prefs.Positions["files"] = (0.5, 0.5);
        prefs.Positions["C:\\Users\\nome\\segredo.pdf"] = (0.1, 0.1);

        var text = prefs.Serialize();

        Assert.DoesNotContain("segredo", text);
        Assert.Matches(@"^(scale=[0-9.]+|position\.[a-z]+=[0-9.]+;[0-9.]+|\s)*$", text);
    }

    [Fact]
    public void Posicao_em_fracao_cabe_em_qualquer_area()
    {
        var (fx, fy) = MenuPlacement.ToFraction(1600, 700, 300, 300, 0, 0, 1920, 1040);
        var (left, top) = MenuPlacement.FromFraction(fx, fy, 300, 300, 0, 0, 1366, 728);

        Assert.InRange(left, 0, 1366 - 300);
        Assert.InRange(top, 0, 728 - 300);
        Assert.Equal((0, 0), MenuPlacement.FromFraction(0, 0, 300, 300, 0, 0, 1920, 1040));
        Assert.Equal((1620, 740), MenuPlacement.FromFraction(1, 1, 300, 300, 0, 0, 1920, 1040));
    }

    [Fact]
    public void Janela_maior_que_a_area_fica_no_canto_de_cima()
    {
        Assert.Equal((100, 50), MenuPlacement.FromFraction(0.7, 0.7, 2000, 2000, 100, 50, 1920, 1040));
    }
}
