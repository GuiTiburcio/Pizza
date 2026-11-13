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

    [Theory]
    [InlineData("pages", new[] { "escritura - pág 01.pdf", "escritura - pág 02.pdf", "escritura - pág 03.pdf" })]
    [InlineData("half", new[] { "escritura - parte 1.pdf", "escritura - parte 2.pdf" })]
    public void Dividir_grava_cada_parte_ao_lado_com_o_nome_do_residente(string mode, string[] expected)
    {
        var source = Path.Combine(_folder, "escritura.pdf");
        File.WriteAllBytes(source, Pages(3));
        var before = SHA256.HashData(File.ReadAllBytes(source));

        var result = Runner().Run(source, "pdf.split", "pdf", new Dictionary<string, string> { ["mode"] = mode }, parts: (part, count) => OutputNaming.PartSuffix(mode, part, count));

        Assert.Null(result.ErrorCode);
        Assert.Equal(expected[0], result.OutputName);
        Assert.Equal(expected.Length, result.Count);
        Assert.All(expected, name => Assert.StartsWith("%PDF-", Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(_folder, name)).AsSpan(0, 5))));
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(source)));
        Assert.Equal(expected.Length + 1, Directory.GetFiles(_folder, "*", new EnumerationOptions { AttributesToSkip = 0 }).Length);
    }

    [Fact]
    public void Juntar_grava_um_pdf_ao_lado_do_primeiro_sem_tocar_nos_originais()
    {
        // ADR-033: as outras entradas chegam ao Worker em AppContainer só como handles.
        var first = Path.Combine(_folder, "escritura.pdf");
        var second = Path.Combine(_folder, "anexo.pdf");
        File.WriteAllBytes(first, Pages(2));
        File.WriteAllBytes(second, Pages(3));
        var before = SHA256.HashData([.. File.ReadAllBytes(first), .. File.ReadAllBytes(second)]);

        var result = Runner().Run(first, "pdf.merge", "pdf", suffix: " - juntado", moreSources: [second]);

        Assert.Null(result.ErrorCode);
        Assert.Equal("escritura - juntado.pdf", result.OutputName);
        var merged = File.ReadAllBytes(Path.Combine(_folder, result.OutputName!));
        Assert.StartsWith("%PDF-", Encoding.Latin1.GetString(merged.AsSpan(0, 5)));
        Assert.Equal(before, SHA256.HashData([.. File.ReadAllBytes(first), .. File.ReadAllBytes(second)]));
        Assert.Equal(3, Directory.GetFiles(_folder, "*", new EnumerationOptions { AttributesToSkip = 0 }).Length);
    }

    [Fact]
    public void Juntar_leva_a_zona_mais_restritiva_entre_os_arquivos()
    {
        // SEC-35: o primeiro veio da intranet (1) e o segundo da internet (3); a saída fica marcada como internet.
        var first = Path.Combine(_folder, "escritura.pdf");
        var second = Path.Combine(_folder, "anexo.pdf");
        File.WriteAllBytes(first, Pages(1));
        File.WriteAllBytes(second, Pages(1));
        File.WriteAllText(first + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=1\r\n");
        File.WriteAllText(second + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://site.example/anexo.pdf\r\n");

        var result = Runner().Run(first, "pdf.merge", "pdf", suffix: " - juntado", moreSources: [second]);

        Assert.Null(result.ErrorCode);
        Assert.Equal("[ZoneTransfer]\r\nZoneId=3\r\n", File.ReadAllText(Path.Combine(_folder, result.OutputName!) + ":Zone.Identifier"));
    }

    [Fact]
    public void Juntar_com_um_arquivo_corrompido_nao_grava_nada()
    {
        var first = Path.Combine(_folder, "escritura.pdf");
        var second = Path.Combine(_folder, "falso.pdf");
        File.WriteAllBytes(first, Pages(1));
        File.WriteAllText(second, "isto não é um PDF");

        var result = Runner().Run(first, "pdf.merge", "pdf", suffix: " - juntado", moreSources: [second]);

        Assert.Equal("CORRUPTED", result.ErrorCode);
        Assert.Equal(2, Directory.GetFiles(_folder, "*", new EnumerationOptions { AttributesToSkip = 0 }).Length);
    }

    [Fact]
    public void Pacote_malformado_de_um_worker_hostil_nao_grava_nada()
    {
        // SEC-13/SEC-30: o residente confere o pacote; fora do formato, nenhuma parte e nenhum temporário ficam.
        var source = Path.Combine(_folder, "escritura.pdf");
        File.WriteAllBytes(source, Pages(2));

        var result = new ConversionRunner(fixture.ProbePath, SandboxFixture.Profile, new TempJournal(_folder + ".journal"), TimeSpan.FromSeconds(60), "--act bad-bundle")
            .Run(source, "pdf.split", "pdf", parts: (part, count) => OutputNaming.PartSuffix("pages", part, count));

        Assert.Equal("WORKER_CRASHED", result.ErrorCode);
        Assert.Single(Directory.GetFiles(_folder, "*", new EnumerationOptions { AttributesToSkip = 0 }));
    }

    private ConversionRunner Runner() =>
        new(fixture.WorkerPath, SandboxFixture.Profile, new TempJournal(_folder + ".journal"), TimeSpan.FromSeconds(60));

    private static byte[] OnePage() => Pages(1);

    private static byte[] Pages(int count)
    {
        var kids = string.Join(" ", Enumerable.Range(0, count).Select(i => $"{i + 3} 0 R"));
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{kids}] /Count {count} >>",
            .. Enumerable.Range(0, count).Select(_ => "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] >>"),
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
