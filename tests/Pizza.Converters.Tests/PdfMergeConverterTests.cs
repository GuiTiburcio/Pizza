using Pizza.Converters.Pdf;

namespace Pizza.Converters.Tests;

/// <summary>Juntar (ADR-033, prancha 6A-1): um PDF só, na ordem do arraste, regravado e limpo.</summary>
public class PdfMergeConverterTests
{
    private static (string? Error, byte[] Output) Merge(params byte[][] pdfs)
    {
        var sources = pdfs.Select(pdf => (Stream)new MemoryStream(pdf)).ToList();
        using var destination = new MemoryStream();
        var error = new PdfMergeConverter().Convert(sources, destination, new Dictionary<string, string>());
        sources.ForEach(source => source.Dispose());
        return (error, destination.ToArray());
    }

    /// <summary>Páginas com larguras diferentes, para conferir a ordem pela largura.</summary>
    private static byte[] Widths(params int[] widths)
    {
        var kids = string.Join(" ", widths.Select((_, i) => $"{i + 3} 0 R"));
        return TestPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{kids}] /Count {widths.Length} >>",
            .. widths.Select(width => $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} 300] >>"),
        ]);
    }

    [Fact]
    public void Junta_na_ordem_recebida_mantendo_a_ordem_das_paginas()
    {
        var (error, output) = Merge(Widths(101, 102), Widths(201), Widths(301, 302, 303));

        Assert.Null(error);
        var inspection = PdfInspection.Of(output);
        Assert.True(inspection.Valid);
        Assert.Equal(6, inspection.Rotations.Count);
        var order = System.Text.RegularExpressions.Regex.Matches(inspection.Everything, @"/MediaBox \[ 0 0 (\d+) 300 \]").Select(match => match.Groups[1].Value);
        Assert.Equal(["101", "102", "201", "301", "302", "303"], order);
    }

    [Fact]
    public void Giro_herdado_de_cada_arquivo_vai_junto()
    {
        var turned = TestPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 /Rotate 90 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] >>");

        var (error, output) = Merge(TestPdf.Pages(1), turned);

        Assert.Null(error);
        Assert.Equal([0, 90], PdfInspection.Of(output).Rotations);
    }

    [Fact]
    public void Resultado_nao_leva_javascript_nem_anexos_de_nenhum_arquivo()
    {
        // SEC-76 e TST-22.
        var hostile = TestPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /OpenAction 4 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /AA << /O 4 0 R >> /Annots [5 0 R] >>",
            "<< /S /JavaScript /JS (app.alert\\(1\\)) >>",
            "<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /A << /S /Launch /F (cmd.exe) >> >>");

        var (error, output) = Merge(TestPdf.Pages(1), hostile);

        Assert.Null(error);
        var inspection = PdfInspection.Of(output);
        Assert.Empty(inspection.ActiveContent);
        Assert.DoesNotContain("cmd.exe", inspection.Everything);
    }

    [Fact]
    public void Um_arquivo_protegido_recusa_o_juntar_inteiro()
    {
        // SEC-74: nada sai sem a proteção de um dos arquivos.
        var (error, output) = Merge(TestPdf.Pages(1), TestPdf.Encrypt(TestPdf.Pages(1), "segredo"));

        Assert.Equal("PASSWORD", error);
        Assert.Empty(output);
    }

    [Fact]
    public void Arquivo_corrompido_no_meio_recusa_o_juntar_inteiro()
    {
        var (error, output) = Merge(TestPdf.Pages(1), "isto não é PDF"u8.ToArray());

        Assert.Equal("CORRUPTED", error);
        Assert.Empty(output);
    }

    [Fact]
    public void Soma_de_paginas_acima_de_2000_passa_do_limite()
    {
        // SEC-42 vale para o resultado.
        var (error, output) = Merge(TestPdf.Pages(1500), TestPdf.Pages(501));

        Assert.Equal("LIMIT_EXCEEDED", error);
        Assert.Empty(output);
    }

    [Fact]
    public void Um_arquivo_so_nao_tem_o_que_juntar()
    {
        Assert.Equal("UNSUPPORTED", Merge(TestPdf.Pages(2)).Error);
        using var destination = new MemoryStream();
        Assert.Equal("UNSUPPORTED", new PdfMergeConverter().Convert(new MemoryStream(TestPdf.Pages(2)), destination, new Dictionary<string, string>()));
    }
}
