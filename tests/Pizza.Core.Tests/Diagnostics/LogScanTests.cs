using System.Diagnostics;
using Pizza.Core.Diagnostics;

namespace Pizza.Core.Tests.Diagnostics;

/// <summary>TST-09: varredura dos logs por dados proibidos (PRIV-16), com tools/varrer-logs.ps1.</summary>
public sealed class LogScanTests : IDisposable
{
    private static readonly DateTimeOffset Moment = new(2026, 11, 9, 19, 20, 11, 482, TimeSpan.FromHours(-3));

    private readonly string _folder = Directory.CreateTempSubdirectory("pizza-logs-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Tudo_o_que_o_pizza_registra_passa_na_varredura()
    {
        var lines = Enum.GetValues<LogEvent>().Select(logEvent => LogFormat.Line(Moment, logEvent))
            .Append(LogFormat.Line(Moment, LogEvent.MenuOpened, 87));
        WriteLog(lines.ToArray());

        var (code, output) = Scan();

        Assert.True(code == 0, output);
    }

    [Theory]
    [InlineData("""{"ts":"2026-11-09T19:20:11.482-03:00","lvl":"info","evt":"C:\\Users\\ana\\fachada.jpg"}""")]
    [InlineData("""{"ts":"2026-11-09T19:20:11.482-03:00","lvl":"info","evt":"fachada.jpg"}""")]
    [InlineData("""{"ts":"2026-11-09T19:20:11.482-03:00","lvl":"info","evt":"menu.opened","x":120,"y":300}""")]
    [InlineData("""{"ts":"2026-11-09T19:20:11.482-03:00","lvl":"info","evt":"menu.opened","file":"fachada.jpg"}""")]
    [InlineData("""{"ts":"2026-11-09T19:20:11.482-03:00","lvl":"info","evt":"menu.opened","ms":"fachada"}""")]
    [InlineData("""{"ts":"2026-11-09T19:20:11.482-03:00","lvl":"info","evt":"menu.opened","ms":123456}""")]
    [InlineData("""{"ts":"2026-11-09T19:20:11.482-03:00","lvl":"info","evt":"conversion.done","ms":12}""")]
    [InlineData("senha=segredo123")]
    [InlineData("")]
    public void Linha_fora_do_formato_reprova_sem_mostrar_o_conteudo(string forbidden)
    {
        WriteLog(LogFormat.Line(Moment, LogEvent.AppStarted), forbidden);

        var (code, output) = Scan();

        Assert.Equal(1, code);
        Assert.Contains("linha 2", output, StringComparison.Ordinal);
        Assert.DoesNotContain("fachada", output, StringComparison.Ordinal);
        Assert.DoesNotContain("segredo", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Pasta_sem_log_nao_conta_como_aprovada()
    {
        Assert.Equal(2, Scan().Code);
    }

    private void WriteLog(params string[] lines) =>
        File.WriteAllLines(Path.Combine(_folder, LogFormat.FileName(new DateOnly(2026, 11, 9))), lines);

    private (int Code, string Output) Scan()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(folder!.FullName, "Pizza.slnx")))
        {
            folder = folder.Parent;
        }

        var start = new ProcessStartInfo("pwsh") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in new[] { "-NoProfile", "-File", Path.Combine(folder.FullName, "tools", "varrer-logs.ps1"), "-Pasta", _folder })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
