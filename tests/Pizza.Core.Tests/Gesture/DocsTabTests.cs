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

    [Fact]
    public void Sem_o_motor_o_documento_nao_abre_o_menu()
    {
        // Pizza Brotinho antes das conversões simples (etapa 3): sem conversão, o menu não abre (FR-09).
        using var _ = DocumentEngineSwitch.Set(false);

        Assert.False(RadialMenu.CanConvert("contrato.docx"));
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
