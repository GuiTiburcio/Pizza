using Pizza.Converters.Pdf;
using Pizza.Core.Output;

namespace Pizza.Converters.Tests;

/// <summary>Dividir (ADR-033, prancha 6A-1): cada parte é um PDF válido e limpo, dentro do pacote de saídas.</summary>
public sealed class PdfSplitConverterTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "pizza-dividir-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => File.Delete(_file);

    private (string? Error, List<byte[]> Parts) Split(byte[] pdf, string mode)
    {
        using (var source = new MemoryStream(pdf))
        using (var destination = File.Create(_file))
        {
            var error = new PdfSplitConverter().Convert(source, destination, new Dictionary<string, string> { ["mode"] = mode });
            if (error is not null)
            {
                return (error, []);
            }
        }

        using var handle = File.OpenHandle(_file);
        var bytes = File.ReadAllBytes(_file);
        return (null, OutputBundle.Read(handle, 2000).Select(part => bytes[(int)part.Offset..(int)(part.Offset + part.Length)]).ToList());
    }

    [Fact]
    public void Cada_pagina_vira_um_pdf_mantendo_o_giro_herdado()
    {
        // A árvore de páginas dá 90° a todas; a 3ª tem 180° próprio.
        var pdf = TestPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [4 0 R 5 0 R 6 0 R] /Count 3 /Rotate 90 >>",
            "<< /Length 0 >>\nstream\n\nendstream",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Contents 3 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Contents 3 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Contents 3 0 R /Rotate 180 >>");

        var (error, parts) = Split(pdf, "pages");

        Assert.Null(error);
        Assert.Equal(3, parts.Count);
        var inspections = parts.Select(PdfInspection.Of).ToList();
        Assert.All(inspections, inspection => Assert.True(inspection.Valid));
        Assert.Equal([[90], [90], [180]], inspections.Select(inspection => inspection.Rotations.ToArray()));
    }

    [Theory]
    [InlineData(5, new[] { 3, 2 })]
    [InlineData(4, new[] { 2, 2 })]
    [InlineData(2, new[] { 1, 1 })]
    public void Pela_metade_faz_dois_pdfs_com_a_primeira_metade_maior(int pages, int[] expected)
    {
        var (error, parts) = Split(TestPdf.Pages(pages), "half");

        Assert.Null(error);
        Assert.Equal(expected, parts.Select(part => PdfInspection.Of(part).Rotations.Count));
    }

    [Fact]
    public void Campo_de_formulario_continua_visivel_na_parte()
    {
        // ADR-033: a parte leva o campo (widget) com a aparência dele; o /AcroForm do documento não vai junto,
        // então o campo pode deixar de ser editável. Editar formulário depois de dividir fica para uma etapa própria.
        var pdf = TestPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [7 0 R] >> >>",
            "<< /Type /Pages /Kids [4 0 R 5 0 R] /Count 2 >>",
            "<< /Length 0 >>\nstream\n\nendstream",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Contents 3 0 R /Annots [7 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Contents 3 0 R >>",
            "<< /Type /XObject /Subtype /Form /BBox [0 0 80 20] /Length 22 >>\nstream\nBT (Fulano) Tj ET\n    \nendstream",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (nome) /V (Fulano) /Rect [10 10 90 30] /P 4 0 R /AP << /N 6 0 R >> >>");

        var (error, parts) = Split(pdf, "pages");

        Assert.Null(error);
        var first = PdfInspection.Of(parts[0]);
        Assert.True(first.Valid);
        Assert.Contains("/Widget", first.Everything);
        Assert.Matches(@"/AP << /N \d+ 0 R >>", first.Everything);
        Assert.DoesNotContain("/Widget", PdfInspection.Of(parts[1]).Everything);
    }

    [Fact]
    public void Pdf_de_uma_pagina_nao_tem_o_que_dividir()
    {
        Assert.Equal("UNSUPPORTED", Split(TestPdf.Pages(1), "pages").Error);
    }

    [Fact]
    public void Partes_nao_levam_javascript_nem_anexos()
    {
        // SEC-76 e TST-22, também nas páginas copiadas para cada parte.
        var pdf = TestPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /OpenAction 6 0 R >>",
            "<< /Type /Pages /Kids [4 0 R 5 0 R] /Count 2 >>",
            "<< /Length 0 >>\nstream\n\nendstream",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Contents 3 0 R /AA << /O 6 0 R >> /Annots [7 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Contents 3 0 R >>",
            "<< /S /JavaScript /JS (app.alert\\(1\\)) >>",
            "<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /A << /S /Launch /F (cmd.exe) >> >>");

        var (error, parts) = Split(pdf, "pages");

        Assert.Null(error);
        Assert.All(parts, part =>
        {
            var inspection = PdfInspection.Of(part);
            Assert.Empty(inspection.ActiveContent);
            Assert.DoesNotContain("cmd.exe", inspection.Everything);
        });
    }
}
