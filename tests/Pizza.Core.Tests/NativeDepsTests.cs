using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pizza.Core.Tests;

/// <summary>build/native-deps.json é a fonte do SBOM das DLLs nativas; o hash dele tem de ser o mesmo que o build confere.</summary>
public class NativeDepsTests
{
    private static readonly string Root = FindRoot();

    private static readonly HashSet<string> KnownSpdx = ["Apache-2.0", "BSD-3-Clause", "MIT", "MPL-2.0", "LGPL-2.1-or-later"];

    public static TheoryData<string> Ids()
    {
        var data = new TheoryData<string>();
        foreach (var dep in Dependencies())
        {
            data.Add(dep.GetProperty("id").GetString()!);
        }

        return data;
    }

    [Fact]
    public void Cobre_as_dependencias_nativas_do_build()
    {
        var ids = Dependencies().Select(dep => dep.GetProperty("id").GetString()).ToList();

        Assert.Contains("qpdf", ids);
        Assert.Contains("pdfium", ids);
        Assert.Contains("libjxl", ids);
        Assert.Contains("vcruntime", ids);
        Assert.Contains("libreoffice", ids);
        Assert.Contains("apple-alac", ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Campos_obrigatorios_e_validos(string id)
    {
        var dep = Dependencies().Single(d => d.GetProperty("id").GetString() == id);

        foreach (var field in new[] { "nome", "versao", "licenca", "purl", "url", "sha256", "fixadoEm", "escopo" })
        {
            Assert.False(string.IsNullOrWhiteSpace(dep.GetProperty(field).GetString()), $"{id}: {field} vazio");
        }

        Assert.Matches("^[0-9a-f]{64}$", dep.GetProperty("sha256").GetString());
        Assert.StartsWith("https://", dep.GetProperty("url").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("pkg:", dep.GetProperty("purl").GetString(), StringComparison.Ordinal);
        Assert.Contains(dep.GetProperty("escopo").GetString(), new[] { "previa", "jumbo" });
        Assert.NotEmpty(dep.GetProperty("arquivos").EnumerateArray());

        var license = dep.GetProperty("licenca").GetString()!;
        var parts = license.Split([" AND ", " OR "], StringSplitOptions.TrimEntries);
        Assert.All(parts, part => Assert.True(part.StartsWith("LicenseRef-", StringComparison.Ordinal) || KnownSpdx.Contains(part), $"{id}: licença {part} inválida"));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Hash_e_o_mesmo_fixado_no_build(string id)
    {
        var dep = Dependencies().Single(d => d.GetProperty("id").GetString() == id);
        var pin = dep.GetProperty("fixadoEm").GetString()!.Split('#');
        var text = File.ReadAllText(Path.Combine(Root, pin[0]));
        var pattern = pin[0].EndsWith(".csproj", StringComparison.Ordinal)
            ? $"<{pin[1]}>([0-9a-fA-F]{{64}})</{pin[1]}>"
            : $@"\${pin[1]}\s*=\s*'([0-9a-fA-F]{{64}})'";
        var match = Regex.Match(text, pattern);

        Assert.True(match.Success, $"{id}: hash {pin[1]} não encontrado em {pin[0]}");
        Assert.Equal(match.Groups[1].Value.ToLowerInvariant(), dep.GetProperty("sha256").GetString());
    }

    [Fact]
    public void Versoes_do_json_sao_as_do_csproj()
    {
        var csproj = File.ReadAllText(Path.Combine(Root, "src", "Pizza.Converters", "Pizza.Converters.csproj"));

        foreach (var (id, key) in new[] { ("qpdf", "QpdfVersion"), ("pdfium", "PdfiumVersion"), ("libjxl", "JxlVersion") })
        {
            var version = Regex.Match(csproj, $"<{key}>([^<]+)</{key}>").Groups[1].Value;
            var dep = Dependencies().Single(d => d.GetProperty("id").GetString() == id);

            Assert.EndsWith(version, dep.GetProperty("versao").GetString(), StringComparison.Ordinal);
        }
    }

    private static List<JsonElement> Dependencies()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "build", "native-deps.json")));
        return document.RootElement.GetProperty("componentes").EnumerateArray().Select(element => element.Clone()).ToList();
    }

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
}
