using System.Diagnostics;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Pizza.Core.Diagnostics;
using Pizza.Core.Ipc;

namespace Pizza.Integration.Tests;

/// <summary>TST-05: o processo isolado não acessa rede nem arquivos e não cria processos.</summary>
[Collection(SandboxCollection.Name)]
public sealed class SandboxTests(SandboxFixture fixture) : IDisposable
{
    private const string Secret = "conteudo secreto";

    private readonly string _forbidden = CreateForbiddenFile();

    public void Dispose()
    {
        File.Delete(_forbidden);
        File.Delete(_forbidden + ".escrito");
    }

    [Fact]
    public void Controle_sem_sandbox_a_sonda_consegue_ler_e_escrever()
    {
        // Sem este controle, um "denied" poderia ser só defeito da sonda.
        using var probe = Process.Start(new ProcessStartInfo(fixture.ProbePath, $"--stdio --forbidden \"{_forbidden}\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        var results = ParseResults(probe.StandardOutput.ReadToEnd());

        Assert.Equal("allowed", results["read_forbidden"]);
        Assert.Equal("allowed", results["write_forbidden_folder"]);
        Assert.Equal("allowed", results["spawn_process"]);
        Assert.Equal("False", results["is_appcontainer"]);
    }

    [Fact]
    public void No_sandbox_o_token_do_Windows_confirma_o_AppContainer()
    {
        Assert.Equal("True", RunProbeInSandbox()["is_appcontainer"]);
    }

    [Fact]
    public void No_sandbox_o_Worker_trava_sem_chamar_o_WER()
    {
        // PRIV-05 (ADR-032): o Worker herda do residente o modo sem WER antes de rodar qualquer código. Sem essa
        // herança, o Worker sabotado gerou dump mesmo no AppContainer.
        const uint NoGpFaultErrorBox = 0x0002;
        CrashReporting.Suppress(); // como o residente faz ao abrir
        var mode = uint.Parse(RunProbeInSandbox()["error_mode"], System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(NoGpFaultErrorBox, mode & NoGpFaultErrorBox);
    }

    [Theory]
    [InlineData("read_forbidden")]
    [InlineData("list_forbidden_folder")]
    [InlineData("write_forbidden_folder")]
    [InlineData("spawn_process")]
    [InlineData("dns")]
    [InlineData("connect")]
    public void No_sandbox_a_tentativa_e_negada(string attempt)
    {
        Assert.Equal("denied", RunProbeInSandbox()[attempt]);
        Assert.False(File.Exists(_forbidden + ".escrito"));
    }

    [Fact]
    public void No_sandbox_o_arquivo_entregue_por_handle_pode_ser_lido()
    {
        // SEC-22: o único caminho até o conteúdo é o handle duplicado pelo residente.
        var results = RunProbeInSandbox();

        Assert.Equal("allowed", results["read_handle"]);
        Assert.Equal(Secret, results["handle_text"]);
    }

    [Fact]
    public void Worker_responde_convert_pelo_canal()
    {
        using var worker = StartWorker();

        IpcFraming.WriteFrame(worker.Input, IpcCodec.Serialize(new IpcMessage(IpcType.Convert, "0123456789ab")
        {
            ConverterId = "image.bmp",
            SrcHandle = 4,
            DstHandle = 8,
            Options = new Dictionary<string, string>(),
        }));

        var reply = IpcCodec.Parse(IpcFraming.ReadFrame(worker.Output)!);
        Assert.Equal(IpcType.Error, reply.Type);
        Assert.Equal("UNSUPPORTED", reply.Code);
    }

    [Fact]
    public void Worker_encerra_ao_receber_mensagem_invalida()
    {
        // SEC-13.
        using var worker = StartWorker();

        IpcFraming.WriteFrame(worker.Input, Encoding.UTF8.GetBytes("""{"v":1,"type":"invasor"}"""));

        Assert.True(worker.WaitForExit(10_000));
        Assert.NotEqual(0, worker.ExitCode);
        Assert.Null(IpcFraming.ReadFrame(worker.Output));
    }

    [Fact]
    public void Queda_do_Worker_chega_ao_residente_como_fim_do_canal()
    {
        // SEC-24: o residente só vê o canal fechar.
        using var worker = StartWorker();

        worker.Kill();

        Assert.True(worker.WaitForExit(10_000));
        Assert.Null(IpcFraming.ReadFrame(worker.Output));
    }

    [Fact]
    public void Fechar_o_controle_encerra_o_Worker()
    {
        // SEC-23.
        var worker = StartWorker();
        var process = Process.GetProcessById(worker.ProcessId);

        worker.Dispose();

        Assert.True(process.WaitForExit(10_000));
    }

    private SandboxedProcess StartWorker() => SandboxedProcess.Start(fixture.WorkerPath, SandboxFixture.Profile);

    private Dictionary<string, string> RunProbeInSandbox()
    {
        using var probe = SandboxedProcess.Start(fixture.ProbePath, SandboxFixture.Profile, $"--forbidden \"{_forbidden}\"");
        using var source = new FileStream(_forbidden, FileMode.Open, FileAccess.Read, FileShare.Read);

        var handle = probe.DuplicateIntoProcess(source.SafeFileHandle, SandboxedProcess.GenericRead);
        var line = Encoding.UTF8.GetBytes($"src={handle}\n");
        probe.Input.Write(line);

        using var reader = new StreamReader(probe.Output);
        return ParseResults(reader.ReadToEnd());
    }

    private static Dictionary<string, string> ParseResults(string output)
    {
        var results = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts[1]);

        Assert.True(results.ContainsKey("end"), "A sonda não terminou: " + output);
        return results;
    }

    private static string CreateForbiddenFile()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "pizza-teste-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, Secret);
        return path;
    }
}
