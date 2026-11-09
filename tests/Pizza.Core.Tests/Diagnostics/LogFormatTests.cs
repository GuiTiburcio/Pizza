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
