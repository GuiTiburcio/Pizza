using System.Diagnostics;
using System.Text;
using Pizza.Core.Ipc;
using Pizza.Core.Output;

namespace Pizza.Core.Tests.Ipc;

/// <summary>SEC-40/TST-08: o orçamento cobre os nomes expandidos e todas as imagens já nomeadas.</summary>
public sealed class MarkdownFinalizationTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pizza-md-final-" + Guid.NewGuid().ToString("N"));

    public MarkdownFinalizationTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
        File.Delete(_folder + ".journal");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Expansao_excede_orcamento_e_remove_as_imagens_ja_nomeadas(bool unicode)
    {
        var stem = unicode ? "relatório-ação" : "relatorio-final";
        var (path, bundle) = Prepare(stem, string.Concat(Enumerable.Repeat("\uE0001\uE000", 10)));
        using var source = PendingOutput.OpenSource(path);
        using var handle = File.OpenHandle(bundle);
        var journal = new TempJournal(_folder + ".journal");
        var runner = new ConversionRunner("unused.exe", "unused", journal, TimeSpan.FromSeconds(30));

        // O pacote original tem 70 bytes de Markdown + 4 da assinatura PNG. Só a expansão cruza os 100.
        Assert.Throws<ConversionLimitException>(() => runner.CommitMarkdown(path, source, handle, default,
            Stopwatch.StartNew(), null, maxOutputBytes: 100));

        Assert.Equal(["entrada.pzb", stem + ".docx"], Directory.GetFiles(_folder).Select(Path.GetFileName).Order());
        Assert.Equal("origem", File.ReadAllText(path));
        Assert.Equal(0, journal.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Limite_exato_aceita_e_um_byte_a_menos_recusa_a_soma_final(bool exact)
    {
        const string stem = "relatório";
        var (path, bundle) = Prepare(stem, "\uE0001\uE000");
        using var source = PendingOutput.OpenSource(path);
        using var handle = File.OpenHandle(bundle);
        var runner = new ConversionRunner("unused.exe", "unused", new TempJournal(_folder + ".journal"), TimeSpan.FromSeconds(30));
        var total = Encoding.UTF8.GetByteCount(stem + " - imagem 1.png") + 4;

        if (exact)
        {
            var result = runner.CommitMarkdown(path, source, handle, default, Stopwatch.StartNew(), null, total);
            Assert.Null(result.ErrorCode);
            Assert.Equal(stem + " - imagem 1.png", File.ReadAllText(Path.Combine(_folder, stem + ".md")));
            Assert.Equal(total, new FileInfo(Path.Combine(_folder, stem + ".md")).Length
                + new FileInfo(Path.Combine(_folder, stem + " - imagem 1.png")).Length);
        }
        else
        {
            Assert.Throws<ConversionLimitException>(() => runner.CommitMarkdown(path, source, handle, default,
                Stopwatch.StartNew(), null, total - 1));
            Assert.False(File.Exists(Path.Combine(_folder, stem + ".md")));
            Assert.False(File.Exists(Path.Combine(_folder, stem + " - imagem 1.png")));
        }
    }

    private (string Path, string Bundle) Prepare(string stem, string text)
    {
        var path = Path.Combine(_folder, stem + ".docx");
        File.WriteAllText(path, "origem");
        var bundle = Path.Combine(_folder, "entrada.pzb");
        using var output = File.Create(bundle);
        var writer = new OutputBundle.Writer(output, 2, formatted: true);
        writer.Add(PartFormat.Markdown, Encoding.UTF8.GetBytes(text));
        writer.Add(PartFormat.Png, [0x89, (byte)'P', (byte)'N', (byte)'G']);
        writer.Finish();
        return (path, bundle);
    }
}
