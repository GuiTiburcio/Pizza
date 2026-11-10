using System.Buffers.Binary;
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
    public void Opcao_escolhida_chega_ao_conversor_isolado()
    {
        var source = CopySample();

        var result = Runner().Run(source, "image.png", "png", new Dictionary<string, string> { ["resolution"] = "50" });

        Assert.Null(result.ErrorCode);
        var output = File.ReadAllBytes(Path.Combine(_folder, "fachada.png"));
        Assert.Equal(32, BinaryPrimitives.ReadInt32BigEndian(output.AsSpan(16)));
        Assert.Equal(24, BinaryPrimitives.ReadInt32BigEndian(output.AsSpan(20)));
    }

    [Theory]
    [InlineData("image.pdf", "pdf", "25504446")]
    [InlineData("image.webp", "webp", "52494646")]
    [InlineData("image.tiff", "tiff", "49492A00")]
    [InlineData("image.gif", "gif", "47494638")]
    [InlineData("image.ico", "ico", "00000100")]
    public void Cada_formato_funciona_dentro_do_sandbox(string converterId, string extension, string signature)
    {
        // O destino chega ao Worker só com permissão de escrita.
        var result = Runner().Run(CopySample(), converterId, extension);

        Assert.Null(result.ErrorCode);
        var output = File.ReadAllBytes(Path.Combine(_folder, "fachada." + extension));
        Assert.StartsWith(signature, Convert.ToHexString(output), StringComparison.Ordinal);
        AssertNoTemporaries();
    }

    [Fact]
    public void Png_vira_jpg_dentro_do_sandbox()
    {
        var source = CopySample();
        Runner().Run(source, "image.png", "png");
        File.Delete(source);

        var result = Runner().Run(Path.Combine(_folder, "fachada.png"), "image.jpg", "jpg");

        Assert.Null(result.ErrorCode);
        Assert.StartsWith("FFD8FF", Convert.ToHexString(File.ReadAllBytes(Path.Combine(_folder, "fachada.jpg"))), StringComparison.Ordinal);
    }

    [Fact]
    public void Cancelar_para_o_worker_e_nao_deixa_resto()
    {
        // FR-29.
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        var result = Runner(fixture.ProbePath, "--act hang", TimeSpan.FromSeconds(60)).Run(CopySample(), "image.png", "png", cancellation: cancel.Token);

        Assert.Equal("CANCELLED", result.ErrorCode);
        AssertNothingWritten();
    }

    [Fact]
    public void Cancelar_depois_do_done_ainda_impede_a_saida()
    {
        // FR-29: vale até a saída ser confirmada. A sonda responde logo e fica presa na espera de saída, que dura 5 s.
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(2000));

        var result = Runner(fixture.ProbePath, "--act hang-after-done", TimeSpan.FromSeconds(60)).Run(CopySample(), "image.png", "png", cancellation: cancel.Token);

        Assert.Equal("CANCELLED", result.ErrorCode);
        AssertNothingWritten();
    }

    [Fact]
    public void Cancelada_antes_de_comecar_nem_abre_o_worker()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        var result = Runner().Run(CopySample(), "image.png", "png", cancellation: cancel.Token);

        Assert.Equal("CANCELLED", result.ErrorCode);
        AssertNothingWritten();
    }

    [Fact]
    public void Opcao_fora_do_esquema_nao_gera_saida()
    {
        var result = Runner().Run(CopySample(), "image.png", "png", new Dictionary<string, string> { ["resolution"] = "200" });

        Assert.Equal("UNSUPPORTED", result.ErrorCode);
        AssertNothingWritten();
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

    [Fact]
    public void Worker_encerrado_a_forca_nao_deixa_temporario_ao_terminar()
    {
        // SEC-34: encerrar é assíncrono; a limpeza só pode acabar depois de o Worker soltar o handle.
        for (var round = 0; round < 40; round++)
        {
            var source = CopySample();

            Runner(fixture.ProbePath, "--act wrong-job", TimeSpan.FromSeconds(30)).Run(source, "image.png", "png");

            AssertNothingWritten();
            File.Delete(source);
        }
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
