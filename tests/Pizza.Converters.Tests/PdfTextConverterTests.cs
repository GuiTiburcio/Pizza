using System.Text;
using Pizza.Converters.Pdf;

namespace Pizza.Converters.Tests;

/// <summary>Texto (adendo da ADR-033, prancha 6B-1): o texto de cada página, uma após a outra, em UTF-8 com BOM e CRLF.</summary>
public sealed class PdfTextConverterTests
{
    private static (string? Error, byte[] Output) Extract(byte[] pdf)
    {
        using var source = new MemoryStream(pdf);
        using var destination = new MemoryStream();
        var error = new PdfTextConverter().Convert(source, destination, new Dictionary<string, string>());
        return (error, destination.ToArray());
    }

    [Fact]
    public void Tira_o_texto_de_cada_pagina_separado_por_linha_em_branco()
    {
        var (error, output) = Extract(TestPdf.Drawn(2, "Ola Pizza"));

        Assert.Null(error);
        Assert.Equal(Encoding.UTF8.Preamble.ToArray(), output[..3]);
        Assert.Equal("Ola Pizza\r\n\r\nOla Pizza\r\n", Encoding.UTF8.GetString(output.AsSpan(3)));
    }

    [Fact]
    public void Pdf_sem_texto_recebe_no_text_e_nao_grava_nada()
    {
        // Como um PDF digitalizado: só desenho, nenhum caractere.
        var (error, output) = Extract(TestPdf.Drawn(2, blackLeftHalf: true));

        Assert.Equal("NO_TEXT", error);
        Assert.Empty(output);
    }

    [Fact]
    public void Pdf_com_senha_recebe_password()
    {
        Assert.Equal("PASSWORD", Extract(TestPdf.Encrypt(TestPdf.Drawn(1, "Segredo"), "segredo")).Error);
    }

    [Fact]
    public void Arquivo_que_nao_e_pdf_recebe_corrupted()
    {
        Assert.Equal("CORRUPTED", Extract("isto não é um PDF"u8.ToArray()).Error);
    }
}
