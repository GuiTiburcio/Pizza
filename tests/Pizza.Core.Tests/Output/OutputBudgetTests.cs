using Pizza.Core.Output;

namespace Pizza.Core.Tests.Output;

public sealed class OutputBudgetTests
{
    [Fact]
    public void Reescrever_cabecalho_nao_cobra_os_bytes_duas_vezes()
    {
        using var file = new MemoryStream();
        using var output = new OutputBudgetStream(file, 8);
        output.Write(new byte[8]);
        output.Position = 0;
        output.Write("PZB1"u8);
        output.Seek(0, SeekOrigin.End);

        Assert.Equal(8, output.Length);
        Assert.False(output.Exceeded);
        Assert.Throws<ConversionLimitException>(() => output.WriteByte(0));
        Assert.Equal(8, file.Length);
        Assert.True(output.Exceeded);
    }

    [Theory]
    [InlineData("length")]
    [InlineData("position")]
    [InlineData("seek")]
    [InlineData("write")]
    public void Excesso_permanece_marcado_apos_codec_capturar_a_excecao(string operation)
    {
        using var file = new MemoryStream();
        using var output = new OutputBudgetStream(file, 8);
        Assert.Throws<ConversionLimitException>(() =>
        {
            switch (operation)
            {
                case "length": output.SetLength(9); break;
                case "position": output.Position = 9; break;
                case "seek": output.Seek(9, SeekOrigin.Begin); break;
                default: output.Write(new byte[9], 0, 9); break;
            }
        });

        Assert.True(output.Exceeded);
        Assert.Equal(0, file.Length);
        Assert.Throws<ConversionLimitException>(() => output.SetLength(0));
    }

    [Fact]
    public void Lacuna_e_contada_no_comprimento_final()
    {
        using var file = new MemoryStream();
        using var output = new OutputBudgetStream(file, 8);
        output.Position = 7;
        output.WriteByte(1);
        Assert.Equal(8, file.Length);
        Assert.Throws<ConversionLimitException>(() => output.WriteByte(2));
    }

    [Fact]
    public void Teto_publico_e_dois_gib_e_o_envelope_fica_separado()
    {
        Assert.Equal(2_147_483_648L, OutputBudget.MaxBytes);
        Assert.Equal(OutputBudget.MaxBytes, OutputBudget.ContainerBytes(false));
        Assert.Equal(OutputBudget.MaxBytes + 16_008, OutputBudget.ContainerBytes(true));
    }
}
