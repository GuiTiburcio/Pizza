using System.Diagnostics;
using Pizza.Core.Ipc;

namespace Pizza.Integration.Tests;

/// <summary>Copia os executáveis para uma pasta temporária que só o perfil de teste consegue ler (V-03).</summary>
public sealed class SandboxFixture : IDisposable
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    public const string Profile = "Pizza.Worker.Test";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "pizza-sandbox-" + Guid.NewGuid().ToString("N"));

    public SandboxFixture()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Pizza.slnx")))
        {
            repository = repository.Parent;
        }

        Assert.NotNull(repository);
        var binaries = Path.Combine(repository.FullName, "src", "Pizza.Worker", "bin", Configuration, "net10.0-windows", "win-x64");
        var probeBinaries = Path.Combine(repository.FullName, "tests", "Pizza.Sandbox.Probe", "bin", Configuration, "net10.0-windows");

        Copy(binaries, Path.Combine(_root, "worker"));
        Copy(probeBinaries, Path.Combine(_root, "probe"));

        var sid = SandboxedProcess.EnsureProfile(Profile);
        using var grant = Process.Start(new ProcessStartInfo("icacls.exe", $"\"{_root}\" /grant \"*{sid}:(OI)(CI)(RX)\" /T /Q")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        grant.WaitForExit();
        Assert.Equal(0, grant.ExitCode);
    }

    public string WorkerPath => Path.Combine(_root, "worker", "Pizza.Worker.exe");

    public string ProbePath => Path.Combine(_root, "probe", "Pizza.Sandbox.Probe.exe");

    public void Dispose()
    {
        SandboxedProcess.DeleteProfile(Profile);
        Directory.Delete(_root, recursive: true);
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }
    }
}
