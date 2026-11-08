using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

public class TabCarouselTests
{
    private readonly TabCarousel _carousel = new(4);

    [Fact]
    public void Um_dente_para_baixo_avanca_uma_aba()
    {
        Assert.True(_carousel.OnWheel(-120));
        Assert.Equal(1, _carousel.Target);
    }

    [Fact]
    public void Um_dente_para_cima_na_primeira_aba_da_a_volta()
    {
        _carousel.OnWheel(120);
        Assert.Equal(3, _carousel.Target);
    }

    [Fact]
    public void Fracoes_de_dente_so_trocam_ao_completar_um_dente()
    {
        Assert.False(_carousel.OnWheel(-40));
        Assert.False(_carousel.OnWheel(-40));
        Assert.True(_carousel.OnWheel(-40));
        Assert.Equal(1, _carousel.Target);
    }

    [Fact]
    public void Inverter_o_sentido_descarta_a_fracao_acumulada()
    {
        _carousel.OnWheel(-100);
        Assert.False(_carousel.OnWheel(40));
        Assert.False(_carousel.OnWheel(-40));
        Assert.Equal(0, _carousel.Target);
    }

    [Fact]
    public void Chega_ao_destino_em_cerca_de_300_ms()
    {
        _carousel.OnWheel(-120);
        for (var i = 0; i < 20; i++)
        {
            _carousel.Advance(16);
        }

        Assert.False(_carousel.IsAnimating);
        Assert.Equal(1, _carousel.Position);
    }

    [Fact]
    public void A_posicao_so_anda_em_direcao_ao_destino()
    {
        _carousel.OnWheel(-120);
        var previous = _carousel.Position;
        while (_carousel.IsAnimating)
        {
            _carousel.Advance(16);
            Assert.True(_carousel.Position > previous);
            previous = _carousel.Position;
        }
    }

    [Fact]
    public void O_resultado_nao_depende_da_taxa_de_quadros()
    {
        var other = new TabCarousel(4);
        _carousel.OnWheel(-120);
        other.OnWheel(-120);

        _carousel.Advance(48);
        for (var i = 0; i < 3; i++)
        {
            other.Advance(16);
        }

        Assert.Equal(_carousel.Position, other.Position, 6);
    }

    [Fact]
    public void Girar_no_meio_do_deslize_continua_de_onde_esta()
    {
        _carousel.OnWheel(-120);
        _carousel.Advance(30);
        var middle = _carousel.Position;

        _carousel.OnWheel(-120);

        Assert.Equal(middle, _carousel.Position);
        Assert.Equal(2, _carousel.Target);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 1, 1)]
    [InlineData(1, 3, 2)]
    [InlineData(3, 0, 1)]
    [InlineData(0, 3, -1)]
    public void DistanceTo_usa_o_caminho_mais_curto(int notches, int tab, double expected)
    {
        for (var i = 0; i < notches; i++)
        {
            _carousel.OnWheel(-120);
        }

        _carousel.Snap();

        Assert.Equal(Math.Abs(expected), Math.Abs(_carousel.DistanceTo(tab)), 6);
        if (Math.Abs(expected) < 2)
        {
            Assert.Equal(expected, _carousel.DistanceTo(tab), 6);
        }
    }

    [Fact]
    public void Reset_volta_para_a_primeira_aba_sem_animar()
    {
        _carousel.OnWheel(-120);
        _carousel.Reset();

        Assert.Equal(0, _carousel.Target);
        Assert.False(_carousel.IsAnimating);
    }
}
