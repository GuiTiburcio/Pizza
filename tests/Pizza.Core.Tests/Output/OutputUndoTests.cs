using Pizza.Core.Output;

namespace Pizza.Core.Tests.Output;

/// <summary>Desfazer: só o arquivo gerado vai para a Lixeira, e só se ainda for o mesmo (SEC-05, SEC-31).</summary>
public sealed class OutputUndoTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pizza-undo-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    private sealed class FakeBin(bool succeeds = true) : IRecycleBin
    {
        public List<string> Received { get; } = [];

        public bool Recycle(IReadOnlyList<string> paths)
        {
            Received.AddRange(paths);
            return succeeds;
        }
    }

    private string Make(string name, string content = "gerado")
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Envia_so_o_gerado_e_nunca_o_original()
    {
        var source = Make("a.png", "original");
        var output = Make("a.pdf");
        var bin = new FakeBin();

        var undo = OutputUndo.Capture(_folder, ["a.pdf"], [source]);

        Assert.NotNull(undo);
        Assert.Equal(UndoOutcome.Done, undo.Undo(bin));
        Assert.Equal([output], bin.Received);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void Nome_igual_ao_do_original_nao_e_aceito()
    {
        var source = Make("a.png");

        Assert.Null(OutputUndo.Capture(_folder, ["a.png"], [source]));
        Assert.Null(OutputUndo.Capture(_folder, ["A.PNG"], [source]));
    }

    [Theory]
    [InlineData("..\\fora.pdf")]
    [InlineData("sub\\a.pdf")]
    [InlineData("C:\\Windows\\a.pdf")]
    public void Nome_com_caminho_nao_e_aceito(string name)
    {
        Assert.Null(OutputUndo.Capture(_folder, [name], []));
    }

    [Fact]
    public void Arquivo_inexistente_na_captura_nao_oferece_desfazer()
    {
        Assert.Null(OutputUndo.Capture(_folder, ["nao-existe.pdf"], []));
    }

    [Fact]
    public void Arquivo_alterado_depois_da_conversao_nao_e_apagado()
    {
        var output = Make("a.pdf", "curto");
        var undo = OutputUndo.Capture(_folder, ["a.pdf"], [])!;
        File.WriteAllText(output, "agora bem maior que antes");
        var bin = new FakeBin();

        Assert.Equal(UndoOutcome.Changed, undo.Undo(bin));
        Assert.Empty(bin.Received);
        Assert.True(File.Exists(output));
    }

    [Fact]
    public void Mesma_hora_de_gravacao_mas_outro_tamanho_tambem_nao_e_apagado()
    {
        var output = Make("a.pdf", "1234");
        var undo = OutputUndo.Capture(_folder, ["a.pdf"], [])!;
        var stamp = File.GetLastWriteTimeUtc(output);
        File.WriteAllText(output, "12345678");
        File.SetLastWriteTimeUtc(output, stamp);

        Assert.Equal(UndoOutcome.Changed, undo.Undo(new FakeBin()));
    }

    [Fact]
    public void Mesmo_tamanho_com_outra_hora_de_gravacao_nao_e_apagado()
    {
        var output = Make("a.pdf", "1234");
        var undo = OutputUndo.Capture(_folder, ["a.pdf"], [])!;
        File.SetLastWriteTimeUtc(output, File.GetLastWriteTimeUtc(output).AddMinutes(5));

        Assert.Equal(UndoOutcome.Changed, undo.Undo(new FakeBin()));
    }

    [Fact]
    public void Arquivo_que_sumiu_nao_e_enviado()
    {
        var output = Make("a.pdf");
        var undo = OutputUndo.Capture(_folder, ["a.pdf"], [])!;
        File.Delete(output);
        var bin = new FakeBin();

        Assert.Equal(UndoOutcome.Changed, undo.Undo(bin));
        Assert.Empty(bin.Received);
    }

    [Fact]
    public void Varias_partes_vao_todas_juntas()
    {
        var parts = new[] { Make("a - parte 1.pdf"), Make("a - parte 2.pdf"), Make("a - parte 3.pdf") };
        var undo = OutputUndo.Capture(_folder, ["a - parte 1.pdf", "a - parte 2.pdf", "a - parte 3.pdf"], [])!;
        var bin = new FakeBin();

        Assert.Equal(UndoOutcome.Done, undo.Undo(bin));
        Assert.Equal(parts, bin.Received);
    }

    [Fact]
    public void Se_uma_parte_mudou_nenhuma_vai_para_a_lixeira()
    {
        Make("a - parte 1.pdf");
        var second = Make("a - parte 2.pdf", "curto");
        Make("a - parte 3.pdf");
        var undo = OutputUndo.Capture(_folder, ["a - parte 1.pdf", "a - parte 2.pdf", "a - parte 3.pdf"], [])!;
        File.WriteAllText(second, "editado depois pelo usuario");
        var bin = new FakeBin();

        Assert.Equal(UndoOutcome.Changed, undo.Undo(bin));
        Assert.Empty(bin.Received);
    }

    [Fact]
    public void Falha_da_lixeira_e_informada()
    {
        Make("a.pdf");
        var undo = OutputUndo.Capture(_folder, ["a.pdf"], [])!;

        Assert.Equal(UndoOutcome.Failed, undo.Undo(new FakeBin(succeeds: false)));
    }

    [Fact]
    public void Desfazer_so_vale_uma_vez()
    {
        Make("a.pdf");
        var undo = OutputUndo.Capture(_folder, ["a.pdf"], [])!;
        var bin = new FakeBin();

        undo.Undo(bin);

        Assert.Equal(UndoOutcome.Failed, undo.Undo(bin));
        Assert.Single(bin.Received);
    }
}
