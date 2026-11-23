using System.Text;
using Pizza.Converters.Pdf;

namespace Pizza.Converters.Tests;

/// <summary>TST-14, PRIV-30/31: dados pessoais saem; título e conteúdo técnico permanecem.</summary>
public sealed unsafe class PdfPrivacyTests
{
    [Fact]
    public void Xmp_com_alias_e_subtipo_malformado_nao_preserva_bytes_ou_referencias_privadas()
    {
        const string xmp = "<xmp>PizzaAuditStream</xmp>";
        var bytes = TestPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Metadata 4 0 R /PrivateAlias 4 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] >>",
            $"<< /Type /Metadata /Subtype << /Author (PizzaPessoaSintetica) >> /Length {xmp.Length} >>\nstream\n{xmp}\nendstream");
        var output = RotateBytes(bytes);
        Assert.DoesNotContain("PizzaAudit", PdfInspection.Of(output).Everything);
        fixed (byte* source = output)
        {
            var qpdf = Qpdf.Init();
            byte* data = null;
            nuint size = 0;
            try
            {
                byte empty = 0;
                Assert.Equal(0, Qpdf.ReadMemory(qpdf, "saida", source, (ulong)output.Length, &empty) & Qpdf.Errors);
                var alias = Qpdf.Key(qpdf, Qpdf.Root(qpdf), "/PrivateAlias");
                Assert.NotEqual(0, Qpdf.IsStream(qpdf, alias));
                Assert.Equal(0, Qpdf.StreamData(qpdf, alias, 0, null, &data, &size) & Qpdf.Errors);
                Assert.Equal((nuint)0, size); // Confere os bytes, não só o dicionário do stream.
            }
            finally
            {
                Qpdf.FreeBuffer(&data);
                Qpdf.Cleanup(ref qpdf);
            }
        }
    }

    [Theory]
    [InlineData("<!DOCTYPE x [<!ENTITY a 'PizzaPessoaSintetica'>]><x>&a;</x>", false, "CORRUPTED")]
    [InlineData("<broken>", false, "CORRUPTED")]
    [InlineData("<x/>", true, "LIMIT_EXCEEDED")]
    public void Xmp_inseguro_ou_com_expansao_excessiva_e_recusado(string xml, bool oversized, string expected)
    {
        if (oversized)
        {
            xml = "<x>" + new string('a', 1024 * 1024 + 1) + "</x>";
        }

        using var compressed = new MemoryStream();
        using (var zip = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zip.Write(Encoding.UTF8.GetBytes(xml));
        }

        var data = compressed.ToArray();
        var bytes = TestPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Metadata 4 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] >>",
            $"<< /Type /Metadata /Subtype /XML /Filter /FlateDecode /Length {data.Length} >>\nstream\n{Encoding.Latin1.GetString(data)}\nendstream");
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        Assert.Equal(expected, new PdfRotateConverter().Convert(input, output, new Dictionary<string, string> { ["rotation"] = "90" }));
        Assert.Empty(output.ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Comentario_com_referencia_na_arvore_de_estrutura_tambem_e_limpo(bool hasType)
    {
        var bytes = TestPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot << /Type /StructTreeRoot /K << /Type /OBJR /Obj 4 0 R >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Annots [4 0 R] >>",
            $"<< {(hasType ? "/Type /Annot" : "")} /Subtype /Text /Rect [0 0 10 10] /T (PizzaPessoaSintetica) /Contents (PizzaAuditComment) /AP << /N 5 0 R >> >>",
            "<< /Length 12 >>\nstream\nPizzaAuditAP\nendstream");
        var inspection = Rotate(bytes);
        Assert.DoesNotContain("PizzaAudit", inspection.Everything);
        Assert.DoesNotContain("/AP", inspection.Everything);
        Assert.Contains("/Annots [ ]", inspection.Everything);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Titulo_apenas_no_xmp_e_preservado_sem_autor(bool compressed, bool infoTitle)
    {
        const string xmp = "<rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#' xmlns:dc='http://purl.org/dc/elements/1.1/'><rdf:Description><dc:title><rdf:Alt><rdf:li xml:lang='x-default'>XmpTechnicalTitle</rdf:li></rdf:Alt></dc:title><dc:creator>PizzaPessoaSintetica</dc:creator></rdf:Description></rdf:RDF>";
        var data = Encoding.UTF8.GetBytes(xmp);
        if (compressed)
        {
            using var buffer = new MemoryStream();
            using (var zip = new System.IO.Compression.ZLibStream(buffer, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
            {
                zip.Write(data);
            }

            data = buffer.ToArray();
        }

        var bytes = TestPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Metadata 4 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] >>",
            $"<< /Type /Metadata /Subtype /XML /Length {data.Length} {(compressed ? "/Filter /FlateDecode" : "")} >>\nstream\n{Encoding.Latin1.GetString(data)}\nendstream",
            "<< /Title (InfoTechnicalTitle) /Author (PizzaPessoaSintetica) >>");
        if (infoTitle)
        {
            bytes = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(bytes).Replace("/Root 1 0 R >>", "/Root 1 0 R /Info 5 0 R >>", StringComparison.Ordinal));
        }

        var inspection = Rotate(bytes);
        Assert.Equal(infoTitle ? "InfoTechnicalTitle" : "XmpTechnicalTitle", inspection.Title);
        Assert.DoesNotContain("PizzaAudit", inspection.Everything);
        Assert.DoesNotContain("/Metadata", inspection.Everything);
    }

    private static PdfInspection Rotate(byte[] bytes)
        => PdfInspection.Of(RotateBytes(bytes));

    private static byte[] RotateBytes(byte[] bytes)
    {
        using var source = new MemoryStream(bytes);
        using var output = new MemoryStream();
        Assert.Null(new PdfRotateConverter().Convert(source, output, new Dictionary<string, string> { ["rotation"] = "90" }));
        return output.ToArray();
    }

    [Fact]
    public void Girar_remove_info_pessoal_xmp_e_comentarios_preservando_titulo_e_pagina()
    {
        const string xmp = "<x:xmpmeta xmlns:x='adobe:ns:meta/'>PizzaAuditXmpPerson</x:xmpmeta>";
        var bytes = TestPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Metadata 5 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Annots [6 0 R 7 0 R] /Metadata 5 0 R >>",
            "<< /Title (TechnicalTitle) /Author (PizzaAuditAuthor) /Company (PizzaAuditCompany) /Custom (PizzaAuditCustom) >>",
            $"<< /Type /Metadata /Subtype /XML /Length {Encoding.ASCII.GetByteCount(xmp)} >>\nstream\n{xmp}\nendstream",
            "<< /Type /Annot /Subtype /Text /Rect [0 0 10 10] /T (PizzaAuditCommentAuthor) /Contents (PizzaAuditComment) >>",
            "<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /A << /S /URI /URI (https://example.com) >> >>");
        var pdf = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(bytes).Replace("/Root 1 0 R >>", "/Root 1 0 R /Info 4 0 R >>", StringComparison.Ordinal));
        using var source = new MemoryStream(pdf);
        using var destination = new MemoryStream();
        Assert.Null(new PdfRotateConverter().Convert(source, destination, new Dictionary<string, string> { ["rotation"] = "90" }));
        var inspection = PdfInspection.Of(destination.ToArray());
        Assert.True(inspection.Valid);
        Assert.Equal([90], inspection.Rotations);
        Assert.Contains("TechnicalTitle", inspection.Everything);
        Assert.Contains("https://example.com", inspection.Everything);
        Assert.DoesNotContain("PizzaAudit", inspection.Everything);
        Assert.DoesNotContain("/Metadata", inspection.Everything);
        Assert.DoesNotContain("/Subtype /Text", inspection.Everything);
    }
}
