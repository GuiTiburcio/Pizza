using System.Security.Cryptography;
using Pizza.Core.Ipc;
using Pizza.Core.Output;

namespace Pizza.Integration.Tests;

/// <summary>Conversão completa: residente, Worker em sandbox, handles e gravação segura.</summary>
[Collection(SandboxCollection.Name)]
public sealed class ConversionTests(SandboxFixture fixture) : IDisposable
{
    private readonly string _folder = CreateFolder();

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
        File.Delete(_folder + ".journal");
    }

    [Fact]
    public void Jpg_real_vira_png_ao_lado_do_original()
    {
        var source = CopySample();
        var before = SHA256.HashData(File.ReadAllBytes(source));

        var result = Runner().Run(source, "image.png", "png");

        Assert.Null(result.ErrorCode);
        Assert.Equal("fachada.png", result.OutputName);
        var output = File.ReadAllBytes(Path.Combine(_folder, "fachada.png"));
        Assert.True(output.AsSpan().StartsWith((byte[])[0x89, 0x50, 0x4E, 0x47]));
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(source)));
        AssertNoTemporaries();
    }

    [Fact]
    public void Segunda_conversao_nao_sobrescreve()
    {
        var source = CopySample();

        Runner().Run(source, "image.png", "png");
        var second = Runner().Run(source, "image.png", "png");

        Assert.Equal("fachada (1).png", second.OutputName);
    }

    [Theory]
    [InlineData("não é imagem", "UNSUPPORTED")]
    [InlineData("ÿØÿà cabeçalho de jpg e nada mais", "CORRUPTED")]
    public void Arquivo_invalido_nao_gera_saida(string content, string expected)
    {
        var source = Path.Combine(_folder, "ruim.jpg");
        File.WriteAllText(source, content, System.Text.Encoding.Latin1);

        var result = Runner().Run(source, "image.png", "png");

        Assert.Equal(expected, result.ErrorCode);
        Assert.Null(result.OutputName);
        Assert.Equal(["ruim.jpg"], Directory.GetFiles(_folder).Select(Path.GetFileName));
        AssertNoTemporaries();
    }

    [Fact]
    public void Arquivo_aberto_por_outro_programa_e_avisado()
    {
        var source = CopySample();
        using var locked = File.OpenHandle(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.Equal("LOCKED", Runner().Run(source, "image.png", "png").ErrorCode);
    }

    [Fact]
    public void Pasta_sem_permissao_de_escrita_e_avisada()
    {
        var source = CopySample();
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        Icacls($"\"{_folder}\" /deny \"*{user}:(WD)\"");
        try
        {
            Assert.Equal("NO_PERMISSION", Runner().Run(source, "image.png", "png").ErrorCode);
        }
        finally
        {
            Icacls($"\"{_folder}\" /remove:d \"*{user}\"");
        }
    }

    [Fact]
    public void Arquivo_que_sumiu_nao_derruba_o_residente()
    {
        var result = Runner().Run(Path.Combine(_folder, "nao-existe.jpg"), "image.png", "png");

        Assert.Equal("CORRUPTED", result.ErrorCode);
    }

    [Fact]
    public void Conversor_desconhecido_e_recusado()
    {
        Assert.Equal("UNSUPPORTED", Runner().Run(CopySample(), "image.bmp", "bmp").ErrorCode);
    }

    [Fact]
    public void Worker_travado_e_encerrado_pelo_tempo_limite()
    {
        // SEC-23.
        var result = Sabotage("hang", TimeSpan.FromSeconds(2));

        Assert.Equal("TIMEOUT", result.ErrorCode);
        AssertNothingWritten();
    }

    [Fact]
    public void Worker_que_diz_que_terminou_mas_nao_sai_invalida_a_saida()
    {
        // SEC-29 e TST-19: a saída só vale depois que o Worker encerrou.
        var result = Sabotage("hang-after-done", TimeSpan.FromSeconds(30));

        Assert.Equal("WORKER_CRASHED", result.ErrorCode);
        AssertNothingWritten();
    }

    [Fact]
    public void Worker_que_cai_vira_erro_sem_derrubar_o_residente()
    {
        // SEC-24.
        var result = Sabotage("exit", TimeSpan.FromSeconds(30));

        Assert.Equal("WORKER_CRASHED", result.ErrorCode);
        AssertNothingWritten();
    }

    [Fact]
    public void Worker_que_responde_outro_job_e_recusado()
    {
        var result = Sabotage("wrong-job", TimeSpan.FromSeconds(30));

        Assert.Equal("WORKER_CRASHED", result.ErrorCode);
        AssertNothingWritten();
    }

    private ConversionRunner Runner(string? worker = null, string arguments = "", TimeSpan? timeout = null) =>
        new(worker ?? fixture.WorkerPath, SandboxFixture.Profile, new TempJournal(_folder + ".journal"), timeout ?? TimeSpan.FromSeconds(60), arguments);

    private ConversionResult Sabotage(string act, TimeSpan timeout) =>
        Runner(fixture.ProbePath, $"--act {act}", timeout).Run(CopySample(), "image.png", "png");

    private string CopySample()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(repository!.FullName, "Pizza.slnx")))
        {
            repository = repository.Parent;
        }

        var target = Path.Combine(_folder, "fachada.jpg");
        File.Copy(Path.Combine(repository.FullName, "tests", "samples", "fachada.jpg"), target);
        return target;
    }

    private void AssertNoTemporaries() =>
        Assert.Empty(Directory.GetFiles(_folder, ".~pizza-*", new EnumerationOptions { AttributesToSkip = 0 }));

    private void AssertNothingWritten()
    {
        AssertNoTemporaries();
        Assert.False(File.Exists(Path.Combine(_folder, "fachada.png")));
    }

    private static void Icacls(string arguments)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("icacls.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    private static string CreateFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pizza-conversao-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
