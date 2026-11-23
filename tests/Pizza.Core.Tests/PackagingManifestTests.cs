using System.Diagnostics;
using System.Text.Json;

namespace Pizza.Core.Tests;

/// <summary>
/// packaging/*.json declara o que entra em cada edição (ADR-034, etapa 4). Estes testes garantem que os manifestos são
/// válidos, que o Brotinho não leva Pizza.Docs nem o motor e que o Jumbo é superconjunto do Brotinho; e que
/// tools/montar-edicao.ps1 falha com arquivo faltando ou arquivo fora do manifesto.
/// </summary>
public class PackagingManifestTests
{
    private static readonly string Root = FindRoot();
    private static readonly string[] Origins = ["pizza", "docs", "motor", "raiz"];

    public static TheoryData<string> Manifests()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "packaging"), "*.json"))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    [Fact]
    public void Existem_os_tres_manifestos_e_nenhum_binario()
    {
        var files = Directory.GetFiles(Path.Combine(Root, "packaging")).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["README.md", "brotinho.json", "comum.json", "jumbo.json"], files);
    }

    [Theory]
    [MemberData(nameof(Manifests))]
    public void Manifesto_e_valido(string name)
    {
        var manifest = Load(name);

        Assert.Equal(1, manifest.Schema);
        Assert.Equal(name, manifest.Edition);
        Assert.False(string.IsNullOrWhiteSpace(manifest.Description));
        Assert.All(manifest.Inherits, parent => Assert.True(File.Exists(ManifestPath(parent)), $"{name}: herda '{parent}', que não existe"));
        Resolve(name); // lança em herança circular

        foreach (var entry in manifest.Entries)
        {
            Assert.Contains(entry.Origin, Origins);
            if (entry.Tree)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Destination), $"{name}: árvore sem destino");
                Assert.NotEmpty(entry.Requires);
            }
            else
            {
                Assert.NotEmpty(entry.Files);
            }

            foreach (var path in entry.Files.Concat(entry.Requires).Append(entry.Destination ?? "x"))
            {
                Assert.False(Path.IsPathRooted(path) || path.Contains("..", StringComparison.Ordinal) || path.Contains('\\', StringComparison.Ordinal), $"{name}: caminho inválido '{path}'");
            }
        }

        var all = Resolve(name).SelectMany(m => m.Entries).SelectMany(Destinations).ToList();
        Assert.Equal(all.Count, all.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Brotinho_nao_leva_Pizza_Docs_nem_o_motor()
    {
        var chain = Resolve("brotinho");
        var destinations = chain.SelectMany(m => m.Entries).SelectMany(Destinations).ToList();

        Assert.DoesNotContain(chain.SelectMany(m => m.Entries), entry => entry.Origin is "docs" or "motor" || entry.Tree);
        Assert.DoesNotContain(destinations, d => d.StartsWith("Pizza.Docs", StringComparison.OrdinalIgnoreCase) || d.StartsWith("motor/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Pizza.Docs*", Load("brotinho").Forbidden);
        Assert.Contains("motor/*", Load("brotinho").Forbidden);
    }

    [Fact]
    public void Jumbo_e_superconjunto_do_Brotinho_com_Docs_e_motor()
    {
        var brotinho = Resolve("brotinho").SelectMany(m => m.Entries).SelectMany(Destinations).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var jumbo = Resolve("jumbo").SelectMany(m => m.Entries).SelectMany(Destinations).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(brotinho.IsProperSubsetOf(jumbo));
        Assert.Contains("Pizza.Docs.exe", jumbo);
        Assert.Contains("motor/", jumbo);
        Assert.Contains("Pizza.exe", brotinho);
        Assert.Contains("Pizza.Worker.exe", brotinho);
    }

    [Fact]
    public void Arquivo_sentinela_do_motor_e_o_mesmo_do_native_deps()
    {
        var motor = Resolve("jumbo").SelectMany(m => m.Entries).Single(e => e.Origin == "motor");
        using var deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "build", "native-deps.json")));
        var libreoffice = deps.RootElement.GetProperty("componentes").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "libreoffice");
        var files = libreoffice.GetProperty("arquivos").EnumerateArray().Select(f => f.GetString()).ToList();

        Assert.Equal("jumbo", libreoffice.GetProperty("escopo").GetString());
        Assert.All(motor.Requires, required => Assert.Contains(required, files));
    }

    [Fact]
    public void Script_monta_o_Brotinho_e_o_Jumbo_com_pastas_completas()
    {
        using var work = new TempFolder();
        Populate(work, "jumbo");

        var brotinho = RunScript(work, "brotinho", "out-b");
        var jumbo = RunScript(work, "jumbo", "out-j");

        Assert.True(brotinho.ExitCode == 0, brotinho.Output);
        Assert.True(jumbo.ExitCode == 0, jumbo.Output);
        Assert.False(File.Exists(Path.Combine(work.Path, "out-b", "Pizza.Docs.exe")));
        Assert.False(Directory.Exists(Path.Combine(work.Path, "out-b", "motor")));
        Assert.True(File.Exists(Path.Combine(work.Path, "out-j", "Pizza.Docs.exe")));
        Assert.True(File.Exists(Path.Combine(work.Path, "out-j", "motor", "program", "mergedlo.dll")));
        Assert.True(File.Exists(Path.Combine(work.Path, "out-j", "Pizza.exe")));
    }

    [Fact]
    public void Script_falha_quando_o_manifesto_cita_arquivo_que_nao_existe()
    {
        using var work = new TempFolder();
        Populate(work, "brotinho");
        File.Delete(Path.Combine(work.Path, "Pizza", "Pizza.Worker.exe"));

        var result = RunScript(work, "brotinho", "out");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Pizza.Worker.exe", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_falha_quando_aparece_arquivo_fora_do_manifesto()
    {
        using var work = new TempFolder();
        Populate(work, "brotinho");
        File.WriteAllText(Path.Combine(work.Path, "Pizza", "Pizza.Docs.exe"), "x");

        var result = RunScript(work, "brotinho", "out");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("fora do manifesto", result.Output, StringComparison.Ordinal);
        Assert.Contains("Pizza.Docs.exe", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_falha_no_Jumbo_sem_o_motor_extraido()
    {
        using var work = new TempFolder();
        Populate(work, "jumbo");
        File.Delete(Path.Combine(work.Path, "motor", "program", "mergedlo.dll"));

        var result = RunScript(work, "jumbo", "out");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("mergedlo.dll", result.Output, StringComparison.Ordinal);
    }

    private static void Populate(TempFolder work, string edition)
    {
        foreach (var entry in Resolve(edition).SelectMany(m => m.Entries))
        {
            if (entry.Origin == "raiz")
            {
                continue;
            }

            var folder = Path.Combine(work.Path, entry.Origin == "pizza" ? "Pizza" : entry.Origin == "docs" ? "Docs" : "motor");
            foreach (var file in entry.Tree ? entry.Requires : entry.Files)
            {
                var path = Path.Combine(folder, file.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                // Pizza.Core.dll existe nas duas pastas de publicação e tem de ser idêntico.
                File.WriteAllText(path, file.Equals("Pizza.Core.dll", StringComparison.Ordinal) ? "core" : $"conteudo de {file}");
            }
        }

        if (edition == "jumbo")
        {
            File.WriteAllText(Path.Combine(work.Path, "Docs", "Pizza.Core.dll"), "core");
            File.WriteAllText(Path.Combine(work.Path, "Docs", "Pizza.Docs.pdb"), "ignorado");
        }

        File.WriteAllText(Path.Combine(work.Path, "Pizza", "Pizza.pdb"), "ignorado");
    }

    private static (int ExitCode, string Output) RunScript(TempFolder work, string edition, string output)
    {
        var script = Path.Combine(Root, "tools", "montar-edicao.ps1");
        var info = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
        {
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-Edicao", edition,
            "-Pizza", Path.Combine(work.Path, "Pizza"), "-Docs", Path.Combine(work.Path, "Docs"),
            "-Motor", Path.Combine(work.Path, "motor"), "-Saida", Path.Combine(work.Path, output),
        })
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, text);
    }

    private static IEnumerable<string> Destinations(Entry entry) =>
        entry.Tree
            ? [entry.Destination!.TrimEnd('/') + "/"]
            : entry.Files.Select(file => entry.Destination is null ? file : entry.Destination.TrimEnd('/') + "/" + file);

    private static List<Manifest> Resolve(string name, List<string>? path = null)
    {
        path ??= [];
        Assert.DoesNotContain(name, path);
        var manifest = Load(name);
        var chain = new List<Manifest>();
        foreach (var parent in manifest.Inherits)
        {
            foreach (var inherited in Resolve(parent, [.. path, name]))
            {
                if (chain.All(m => m.Edition != inherited.Edition))
                {
                    chain.Add(inherited);
                }
            }
        }

        chain.Add(manifest);
        return chain;
    }

    private static Manifest Load(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ManifestPath(name)));
        var root = document.RootElement;
        return new Manifest(
            root.GetProperty("esquema").GetInt32(),
            root.GetProperty("edicao").GetString()!,
            root.GetProperty("descricao").GetString()!,
            Strings(root, "herda"),
            Strings(root, "proibidos"),
            root.GetProperty("entradas").EnumerateArray().Select(entry => new Entry(
                entry.GetProperty("origem").GetString()!,
                entry.TryGetProperty("destino", out var destination) ? destination.GetString() : null,
                entry.TryGetProperty("arvore", out var tree) && tree.GetBoolean(),
                Strings(entry, "arquivos"),
                Strings(entry, "exige"))).ToList());
    }

    private static List<string> Strings(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.EnumerateArray().Select(item => item.GetString()!).ToList() : [];

    private static string ManifestPath(string name) => Path.Combine(Root, "packaging", name + ".json");

    private static string FindRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Pizza.slnx")))
        {
            folder = folder.Parent;
        }

        Assert.NotNull(folder);
        return folder.FullName;
    }

    private sealed record Manifest(int Schema, string Edition, string Description, List<string> Inherits, List<string> Forbidden, List<Entry> Entries);

    private sealed record Entry(string Origin, string? Destination, bool Tree, List<string> Files, List<string> Requires);

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pizza-pacote-" + Guid.NewGuid().ToString("N"));

        public TempFolder() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
