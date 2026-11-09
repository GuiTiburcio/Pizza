using Pizza.Core.Ipc;

namespace Pizza.Core.Tests.Ipc;

public class ConversionMessagesTests
{
    [Theory]
    [InlineData("CORRUPTED", "O arquivo está corrompido ou não é do tipo esperado")]
    [InlineData("LOCKED", "O arquivo está aberto em outro programa")]
    [InlineData("NO_SPACE", "Não há espaço em disco suficiente")]
    [InlineData("NO_PERMISSION", "Sem permissão para gravar nesta pasta")]
    [InlineData("LIMIT_EXCEEDED", "O arquivo passa do limite permitido (tamanho, páginas ou pixels)")]
    [InlineData("TIMEOUT", "A conversão demorou mais que o limite")]
    [InlineData("UNSUPPORTED", "Este tipo de conversão não é suportado para este arquivo")]
    [InlineData("WORKER_CRASHED", "O conversor parou inesperadamente; o original não foi alterado")]
    public void Mensagem_em_portugues_claro(string code, string expected)
    {
        // SPECS §10.4 e FR-42.
        Assert.Equal(expected, ConversionMessages.For(code));
    }

    [Fact]
    public void Codigo_desconhecido_nao_mostra_o_codigo()
    {
        Assert.Equal("O conversor parou inesperadamente; o original não foi alterado", ConversionMessages.For("OUTRO"));
    }
}
