using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

/// <summary>Faixa do Juntar (prancha 6A-1): os arquivos numerados, na ordem em que vão ficar.</summary>
public class MergeOrderTests
{
    [Fact]
    public void Ate_tres_arquivos_aparecem_todos_numerados()
    {
        Assert.Equal(
            [("1", "escritura.pdf"), ("2", "anexo.pdf"), ("3", "recibo.pdf")],
            MergeOrder.Chips([@"C:\a\escritura.pdf", @"C:\a\anexo.pdf", @"C:\b\recibo.pdf"]));
    }

    [Fact]
    public void Com_mais_de_tres_os_dois_primeiros_e_quantos_faltam()
    {
        Assert.Equal(
            [("1", "a.pdf"), ("2", "b.pdf"), ("+5", "mais 5")],
            MergeOrder.Chips(["a.pdf", "b.pdf", "c.pdf", "d.pdf", "e.pdf", "f.pdf", "g.pdf"]));
    }

    [Fact]
    public void Nome_longo_e_encurtado_mantendo_a_extensao()
    {
        var (_, name) = MergeOrder.Chips(["contrato de prestação de serviços 2026.pdf", "b.pdf"])[0];

        Assert.True(name.Length <= MergeOrder.MaxNameLength);
        Assert.EndsWith("….pdf", name);
        Assert.StartsWith("contrato", name);
    }
}
