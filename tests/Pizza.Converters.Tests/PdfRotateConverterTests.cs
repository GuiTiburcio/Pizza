using Pizza.Converters.Pdf;

namespace Pizza.Converters.Tests;

/// <summary>Girar (ADR-033, prancha 6A-1), com a saída conferida pelo verificador do TST-22.</summary>
public class PdfRotateConverterTests
{
    private static (string? Error, byte[] Output) Rotate(byte[] pdf, string rotation = "90")
    {
        using var source = new MemoryStream(pdf);
        using var destination = new MemoryStream();
        var error = new PdfRotateConverter().Convert(source, destination, new Dictionary<string, string> { ["rotation"] = rotation });
        return (error, destination.ToArray());
    }

    [Theory]
    [InlineData("90", new[] { 270, 180 })]
    [InlineData("180", new[] { 0, 270 })]
    [InlineData("270", new[] { 90, 0 })]
    public void Gira_todas_as_paginas_somando_ao_giro_que_cada_uma_ja_tinha(string rotation, int[] expected)
    {
        // A 1ª página herda 180° da árvore de páginas; a 2ª tem 90° próprio.
        var pdf = TestPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [4 0 R 5 0 R] /Count 2 /Rotate 180 >>",
            "<< /Length 0 >>\nstream\n\nendstream",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Contents 3 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Contents 3 0 R /Rotate 90 >>");

        var (error, output) = Rotate(pdf, rotation);

        Assert.Null(error);
        var inspection = PdfInspection.Of(output);
        Assert.True(inspection.Valid);
        Assert.Equal(expected, inspection.Rotations);
    }

    [Fact]
    public void Saida_nao_leva_javascript_acoes_anexos_nem_midia()
    {
        // SEC-76 e TST-22. O link para um site e a anotação de texto continuam.
        var pdf = TestPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /OpenAction 6 0 R /AA << /WC 6 0 R >> /Names << /JavaScript 7 0 R /EmbeddedFiles 8 0 R >> /AcroForm << /Fields [] /XFA 3 0 R >> >>",
            "<< /Type /Pages /Kids [4 0 R] /Count 1 >>",
            "<< /Length 0 >>\nstream\n\nendstream",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Contents 3 0 R /AA << /O 6 0 R >> /Annots [10 0 R 11 0 R 12 0 R 13 0 R 14 0 R] >>",
            "<< /Type /EmbeddedFile /Length 5 >>\nstream\nhello\nendstream",
            "<< /Type /Action /S /JavaScript /JS (app.alert\\(1\\)) >>",
            "<< /Names [(a) 6 0 R] >>",
            "<< /Names [(f.exe) 9 0 R] >>",
            "<< /Type /Filespec /F (f.exe) /EF << /F 5 0 R >> >>",
            "<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /A << /S /Launch /F (cmd.exe) >> >>",
            "<< /Type /Annot /Subtype /FileAttachment /Rect [0 0 10 10] /FS 9 0 R >>",
            "<< /Type /Annot /Subtype /RichMedia /Rect [0 0 10 10] >>",
            "<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /A << /S /URI /URI (https://example.com) >> >>",
            "<< /Type /Annot /Subtype /Text /Rect [0 0 10 10] /Contents (nota) /A << /S /SubmitForm /F (https://example.com) >> >>");

        var (error, output) = Rotate(pdf);

        Assert.Null(error);
        var inspection = PdfInspection.Of(output);
        Assert.True(inspection.Valid);
        Assert.Empty(inspection.ActiveContent);
        Assert.Contains("/URI", inspection.Everything);
        Assert.Contains("/Text", inspection.Everything);
        Assert.DoesNotContain("cmd.exe", inspection.Everything);
        Assert.DoesNotContain("app.alert", inspection.Everything);
    }

    [Fact]
    public void Acao_perigosa_escondida_em_objeto_de_outra_geracao_tambem_sai()
    {
        // A busca segue as referências a partir do trailer, então a geração do objeto não importa.
        var text = System.Text.Encoding.Latin1.GetString(TestPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /OpenAction 5 0 R >>",
            "<< /Type /Pages /Kids [4 0 R] /Count 1 >>",
            "<< /Length 0 >>\nstream\n\nendstream",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Contents 3 0 R /Annots [6 0 R] >>",
            "<< /S /JavaScript /JS (oculto) >>",
            "<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /A 7 1 R >>",
            "<< /S /Launch /F (calc.exe) >>"));
        var pdf = System.Text.Encoding.Latin1.GetBytes(text.Replace("7 0 obj", "7 1 obj").Replace("00000 n \ntrailer", "00001 n \ntrailer"));

        var (error, output) = Rotate(pdf);

        Assert.Null(error);
        var inspection = PdfInspection.Of(output);
        Assert.Empty(inspection.ActiveContent);
        Assert.DoesNotContain("calc.exe", inspection.Everything);
        Assert.DoesNotContain("oculto", inspection.Everything);
    }

    [Fact]
    public void Pdf_com_senha_e_recusado_sem_gravar_nada()
    {
        // SEC-74 enquanto o campo de senha (FR-24) não existe: nada sai sem proteção.
        var (error, output) = Rotate(TestPdf.Encrypt(TestPdf.Pages(1), "segredo"));

        Assert.Equal("PASSWORD", error);
        Assert.Empty(output);
    }

    [Fact]
    public void Arquivo_que_nao_e_pdf_vira_corrompido()
    {
        var (error, output) = Rotate("isto não é um PDF"u8.ToArray());

        Assert.Equal("CORRUPTED", error);
        Assert.Empty(output);
    }

    [Fact]
    public void Mais_de_2000_paginas_passa_do_limite()
    {
        // SEC-42.
        var (error, output) = Rotate(TestPdf.Pages(PdfDocument.MaxPages + 1));

        Assert.Equal("LIMIT_EXCEEDED", error);
        Assert.Empty(output);
    }

    [Fact]
    public void Exatamente_2000_paginas_ainda_gira()
    {
        var (error, output) = Rotate(TestPdf.Pages(PdfDocument.MaxPages));

        Assert.Null(error);
        Assert.Equal(PdfDocument.MaxPages, PdfInspection.Of(output).Rotations.Count);
    }
}
