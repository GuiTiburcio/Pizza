using Pizza.Core.Visual;

namespace Pizza.Core.Tests.Visual;

// Texto das fatias seguindo a curva (escolha "B · Curva completa" do mantenedor, prancha Ref-Texto-Fatias).
public class CurvedTextTests
{
    private static readonly double[] ThreeLetters = [5, 15, 25];

    [Fact]
    public void Letra_do_meio_fica_no_centro_da_fatia_de_cima_e_de_pe()
    {
        var glyphs = CurvedText.Place(-90, 130, ThreeLetters, 30);

        Assert.Equal(0, glyphs[1].X, 6);
        Assert.Equal(-130, glyphs[1].Y, 6);
        Assert.Equal(0, glyphs[1].Rotation, 6);
    }

    [Fact]
    public void Na_fatia_de_cima_o_texto_corre_da_esquerda_para_a_direita()
    {
        var glyphs = CurvedText.Place(-90, 130, ThreeLetters, 30);

        Assert.True(glyphs[0].X < glyphs[1].X && glyphs[1].X < glyphs[2].X);
    }

    [Fact]
    public void Na_fatia_de_baixo_o_texto_vira_para_ficar_de_pe()
    {
        // Sem a inversão, a palavra apareceria de cabeça para baixo e de trás para a frente.
        var glyphs = CurvedText.Place(90, 130, ThreeLetters, 30);

        Assert.Equal(0, glyphs[1].X, 6);
        Assert.Equal(130, glyphs[1].Y, 6);
        Assert.Equal(0, glyphs[1].Rotation, 6);
        Assert.True(glyphs[0].X < glyphs[1].X && glyphs[1].X < glyphs[2].X);
    }

    [Theory]
    [InlineData(-30)]
    [InlineData(30)]
    [InlineData(150)]
    [InlineData(210)]
    public void Nas_laterais_nenhuma_letra_fica_de_cabeca_para_baixo(double angle)
    {
        foreach (var glyph in CurvedText.Place(angle, 130, ThreeLetters, 30))
        {
            var normalized = ((glyph.Rotation % 360) + 540) % 360 - 180;
            Assert.InRange(normalized, -90, 90);
        }
    }

    [Fact]
    public void Letras_ficam_sobre_o_raio_pedido()
    {
        foreach (var glyph in CurvedText.Place(30, 142, ThreeLetters, 30))
        {
            Assert.Equal(142, Math.Sqrt((glyph.X * glyph.X) + (glyph.Y * glyph.Y)), 6);
        }
    }

    [Theory]
    [InlineData(-90, true, 143, 121)]
    [InlineData(90, false, 120, 142)]
    public void Nome_fica_acima_da_descricao_na_leitura(double angle, bool top, double name, double caption)
    {
        // Em cima, "acima" é para fora do disco; embaixo, a fatia está virada e "acima" é para o miolo.
        Assert.Equal(top, CurvedText.IsTopHalf(angle));
        Assert.Equal(name, CurvedText.NameRadius(angle, 0), 6);
        Assert.Equal(caption, CurvedText.CaptionRadius(angle, 0), 6);
    }

    [Fact]
    public void No_hover_o_texto_acompanha_a_fatia_que_cresce()
    {
        // WF-2: a fatia cresce 6% e sai 5 px; o texto vai junto.
        Assert.Equal((143 * 1.06) + 5, CurvedText.NameRadius(-90, 1), 6);
    }
}
