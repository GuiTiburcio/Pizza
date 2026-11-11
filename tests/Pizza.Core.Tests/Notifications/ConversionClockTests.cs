using Pizza.Core.Notifications;

namespace Pizza.Core.Tests.Notifications;

/// <summary>TST-01 / ADR-029: tempo ativo, estimativa do que falta e texto do aviso.</summary>
public class ConversionClockTests
{
    [Fact]
    public void Antes_de_5_por_cento_ou_de_1_s_ainda_calcula()
    {
        var clock = new ConversionClock(0);
        clock.Report(3);
        Assert.Equal("calculando o tempo…", clock.Text(5000));

        clock.Report(40);
        Assert.Equal("calculando o tempo…", clock.Text(800));
    }

    [Fact]
    public void Estima_o_que_falta_pela_taxa_ate_agora()
    {
        var clock = new ConversionClock(0);
        clock.Report(62);

        Assert.Equal("13 s até agora · falta ~8 s", clock.Text(13_000));
    }

    [Fact]
    public void Pausa_nao_conta_no_tempo_e_aparece_no_texto()
    {
        var clock = new ConversionClock(0);
        clock.Report(50);
        clock.Pause(10_000);
        Assert.Equal("pausado · 10 s até agora", clock.Text(60_000));

        clock.Resume(60_000);
        Assert.Equal(12_000, clock.ActiveMs(62_000));
    }

    [Fact]
    public void Porcentagem_nunca_volta_nem_passa_de_100()
    {
        var clock = new ConversionClock(0);
        clock.Report(40);
        clock.Report(20);
        Assert.Equal(40, clock.Percent);

        clock.Report(150);
        Assert.Equal(100, clock.Percent);
    }

    [Theory]
    [InlineData(21_000, "levou 21 s")]
    [InlineData(65_000, "levou 1 min 05 s")]
    [InlineData(400, "levou menos de 1 s")]
    public void Ao_concluir_mostra_quanto_levou(long now, string expected)
    {
        Assert.Equal(expected, new ConversionClock(0).Took(now));
    }
}
