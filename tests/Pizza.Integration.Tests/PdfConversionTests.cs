using System.Security.Cryptography;
using System.Text;
using Pizza.Core.Ipc;
using Pizza.Core.Output;

namespace Pizza.Integration.Tests;

/// <summary>Aba PDF de ponta a ponta (ADR-033): a QPDF carrega no Worker em AppContainer e o original não muda.</summary>
[Collection(SandboxCollection.Name)]
public sealed class PdfConversionTests(SandboxFixture fixture) : IDisposable
{
    private readonly string _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pizza-pdf-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
        File.Delete(_folder + ".journal");
    }

    [Fact]
    public void Girar_grava_ao_lado_com_sufixo_e_nao_toca_no_original()
    {
        var source = Path.Combine(_folder, "escritura.pdf");
        File.WriteAllBytes(source, OnePage());
        var before = SHA256.HashData(File.ReadAllBytes(source));

        var first = Runner().Run(source, "pdf.rotate", "pdf", new Dictionary<string, string> { ["rotation"] = "90" }, suffix: " - girado");
        var second = Runner().Run(source, "pdf.rotate", "pdf", new Dictionary<string, string> { ["rotation"] = "90" }, suffix: " - girado");

        Assert.Null(first.ErrorCode);
        Assert.Equal("escritura - girado.pdf", first.OutputName);
        Assert.Equal("escritura - girado (1).pdf", second.OutputName);
        Assert.StartsWith("%PDF-", Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(_folder, first.OutputName!)).AsSpan(0, 5)));
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(source)));
        Assert.Empty(Directory.GetFiles(_folder, ".~pizza-*", new EnumerationOptions { AttributesToSkip = 0 }));
    }

    [Fact]
    public void Arquivo_pdf_que_nao_e_pdf_nao_gera_saida()
    {
        var source = Path.Combine(_folder, "falso.pdf");
        File.WriteAllText(source, "isto não é um PDF");

        var result = Runner().Run(source, "pdf.rotate", "pdf", suffix: " - girado");

        Assert.Equal("CORRUPTED", result.ErrorCode);
        Assert.Single(Directory.GetFiles(_folder, "*", new EnumerationOptions { AttributesToSkip = 0 }));
    }

    private ConversionRunner Runner() =>
        new(fixture.WorkerPath, SandboxFixture.Profile, new TempJournal(_folder + ".journal"), TimeSpan.FromSeconds(60));

    private static byte[] OnePage()
    {
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] >>",
        ];
        var text = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(text.Length);
            text.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var start = text.Length;
        text.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        offsets.ForEach(offset => text.Append($"{offset:D10} 00000 n \n"));
        text.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(text.ToString());
    }
}
