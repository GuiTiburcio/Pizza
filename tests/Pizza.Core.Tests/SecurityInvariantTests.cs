namespace Pizza.Core.Tests;

/// <summary>Invariantes de segurança do CLAUDE.md, verificados no código-fonte (TST-01).</summary>
public class SecurityInvariantTests
{
    private static readonly IReadOnlyList<(string Path, string Text)> Sources = LoadSources();

    [Theory]
    [InlineData("WH_KEYBOARD_LL")] // SEC-01
    [InlineData("WH_KEYBOARD")] // SEC-01
    [InlineData("DROPEFFECT_MOVE")] // SEC-05
    [InlineData("HttpClient")] // PRIV-02: nenhuma rede nesta fase
    public void Codigo_nao_contem_o_recurso_proibido(string forbidden)
    {
        Assert.DoesNotContain(Sources, source => source.Text.Contains(forbidden, StringComparison.Ordinal));
    }

    [Fact]
    public void Ctrl_e_lido_em_um_unico_lugar()
    {
        // SEC-02: nenhuma outra leitura de teclado no código.
        var reads = Sources.Sum(source => source.Text.Split("GetAsyncKeyState(VK_CONTROL)").Length - 1);

        Assert.Equal(1, reads);
    }

    private static List<(string, string)> LoadSources()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Pizza.slnx")))
        {
            folder = folder.Parent;
        }

        Assert.NotNull(folder);
        return Directory.EnumerateFiles(Path.Combine(folder.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => (path, File.ReadAllText(path)))
            .ToList();
    }
}
