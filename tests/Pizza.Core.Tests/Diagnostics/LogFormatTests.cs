using Pizza.Core.Diagnostics;

namespace Pizza.Core.Tests.Diagnostics;

public class LogFormatTests
{
    private static readonly DateTimeOffset Moment = new(2026, 11, 8, 19, 20, 11, 482, TimeSpan.FromHours(-3));

    [Fact]
    public void Linha_tem_so_data_e_evento()
    {
        // PRIV-16: o tipo do parâmetro é um enum, então caminho e coordenadas não têm como entrar.
        Assert.Equal(
            """{"ts":"2026-11-08T19:20:11.482-03:00","lvl":"info","evt":"menu.opened"}""",
            LogFormat.Line(Moment, LogEvent.MenuOpened));
    }

    [Fact]
    public void Menu_aberto_pode_levar_so_o_tempo_do_gesto_em_ms()
    {
        // NFR-03: um número, nunca texto (PRIV-16); limitado a 5 dígitos.
        Assert.Equal(
            """{"ts":"2026-11-08T19:20:11.482-03:00","lvl":"info","evt":"menu.opened","ms":87}""",
            LogFormat.Line(Moment, LogEvent.MenuOpened, 87));
        Assert.EndsWith("""
"ms":99999}
""".Trim(), LogFormat.Line(Moment, LogEvent.MenuOpened, 250000));
        Assert.EndsWith("""
"ms":0}
""".Trim(), LogFormat.Line(Moment, LogEvent.MenuOpened, -5));
    }

    [Fact]
    public void Tempo_so_vale_para_o_menu_aberto()
    {
        Assert.Throws<ArgumentException>(() => LogFormat.Line(Moment, LogEvent.ConversionDone, 10));
    }

    [Fact]
    public void Todo_evento_tem_nome_proprio()
    {
        var names = Enum.GetValues<LogEvent>().Select(LogFormat.Name).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, name => Assert.Matches("^[a-z]+\\.[a-z]+$", name));
    }

    [Fact]
    public void Arquivo_do_dia_leva_a_data_no_nome()
    {
        Assert.Equal("pizza-20261108.log", LogFormat.FileName(new DateOnly(2026, 11, 8)));
    }

    [Theory]
    [InlineData("pizza-20261009.log", false)] // exatamente 30 dias: fica
    [InlineData("pizza-20261008.log", true)] // 31 dias: sai
    [InlineData("pizza-20261108.log", false)]
    [InlineData("outro-arquivo.txt", false)] // nunca apaga o que não é nosso
    [InlineData("pizza-abcdefgh.log", false)]
    public void Retencao_de_30_dias(string fileName, bool expired)
    {
        // PRIV-14.
        Assert.Equal(expired, LogFormat.IsExpired(fileName, new DateOnly(2026, 11, 8)));
    }
}
