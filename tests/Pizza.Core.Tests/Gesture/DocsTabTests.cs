using Pizza.Core.Conversions;
using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

/// <summary>ADR-034: a aba Documento converte para PDF quando o motor do Pizza Jumbo está presente.</summary>
[Collection(DocumentEngineSwitch.Name)]
public class DocsTabTests
{
    [Theory]
    [InlineData("contrato.docx")]
    [InlineData("planilha.xlsx")]
    [InlineData("apresentacao.pptx")]
    [InlineData("texto.odt")]
    [InlineData("antigo.doc")]
    public void Com_o_motor_o_documento_abre_na_aba_Documento_com_PDF(string file)
    {
        using var _ = DocumentEngineSwitch.Set(true);

        var pages = RadialMenu.Pages(file);
        var first = pages[RadialMenu.FirstPage(file)];

        Assert.Equal(ConverterCatalog.DocsTab, first.Tab);
        Assert.Contains(RadialMenu.Slices(ConverterCatalog.DocsTab, file), slice => slice.Id == "document.pdf" && slice.Label == "PDF");
    }

    [Theory]
    [InlineData("contrato.docx", "document.txt", "TXT")]
    [InlineData("planilha.xlsx", "document.csv", "CSV")]
    public void Sem_o_motor_o_Brotinho_faz_as_conversoes_simples(string file, string id, string label)
    {
        // ADR-034, etapa 3: DOCX → TXT e XLSX → CSV no Worker, sem o motor; PDF fica só no Pizza Jumbo.
        using var _ = DocumentEngineSwitch.Set(false);

        var slices = RadialMenu.Slices(ConverterCatalog.DocsTab, file);
        Assert.Contains(slices, slice => slice.Id == id && slice.Label == label);
        Assert.DoesNotContain(slices, slice => slice.Id == "document.pdf");
        Assert.Equal(ConverterCatalog.DocsTab, RadialMenu.Pages(file)[RadialMenu.FirstPage(file)].Tab);
    }

    [Theory]
    [InlineData("apresentacao.pptx")]
    [InlineData("antigo.doc")]
    public void Sem_o_motor_documento_sem_conversao_simples_nao_abre_o_menu(string file)
    {
        // FR-09: sem conversão, o menu não abre.
        using var _ = DocumentEngineSwitch.Set(false);

        Assert.False(RadialMenu.CanConvert(file));
    }

    [Fact]
    public void Conversoes_simples_nao_tem_painel_e_rodam_no_Worker()
    {
        // FR-21: sem opções, começa ao soltar.
        Assert.All(["document.txt", "document.csv"], id =>
        {
            var converter = ConverterCatalog.Find(id)!;
            Assert.False(converter.HasPanel);
            Assert.False(converter.DocumentEngine);
        });
    }

    [Fact]
    public void Imagens_nao_dependem_do_motor()
    {
        using var _ = DocumentEngineSwitch.Set(false);

        Assert.True(RadialMenu.CanConvert("foto.jpg"));
    }

    [Fact]
    public void Conversao_de_documento_nao_tem_painel()
    {
        // FR-21: sem opções, começa ao soltar.
        Assert.False(ConverterCatalog.Find("document.pdf")!.HasPanel);
        Assert.True(ConverterCatalog.Find("document.pdf")!.DocumentEngine);
    }
}

/// <summary>O motor é um estado do processo inteiro: os testes que o trocam não rodam em paralelo.</summary>
[CollectionDefinition(Name)]
public sealed class DocumentEngineSwitch
{
    public const string Name = "Motor de documentos";

    public static IDisposable Set(bool available)
    {
        var previous = ConverterCatalog.DocumentEngineAvailable;
        ConverterCatalog.DocumentEngineAvailable = available;
        return new Restore(previous);
    }

    private sealed class Restore(bool previous) : IDisposable
    {
        public void Dispose() => ConverterCatalog.DocumentEngineAvailable = previous;
    }
}
