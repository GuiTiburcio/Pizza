using Pizza.Core.Conversions;
using Pizza.Core.Notifications;

namespace Pizza.Core.Tests.Notifications;

/// <summary>FR-44: lista só em memória, desde que o Pizza abriu.</summary>
public class ConversionHistoryTests
{
    private static ConverterInfo Png => ConverterCatalog.Find("image.png")!;

    private static ConversionHistory With(int count)
    {
        var history = new ConversionHistory();
        for (var id = 1; id <= count; id++)
        {
            history.Add(id, $"C:\\a\\{id}.jpg", Png, new Dictionary<string, string>());
        }

        return history;
    }

    private static ConversionHistory Finished(int count)
    {
        var history = new ConversionHistory();
        for (var id = 1; id <= count; id++)
        {
            history.Add(id, $"C:\\a\\{id}.jpg", Png, new Dictionary<string, string>());
            history.Complete(id, $"C:\\a\\{id}.png");
        }

        return history;
    }

    [Fact]
    public void As_mais_novas_vem_primeiro()
    {
        Assert.Equal([3L, 2L, 1L], With(3).Items.Select(entry => entry.Id));
    }

    [Fact]
    public void Guarda_so_as_100_mais_recentes()
    {
        var history = Finished(103);

        Assert.Equal(100, history.Items.Count);
        Assert.Equal(103, history.Items[0].Id);
        Assert.Equal(4, history.Items[^1].Id);
        Assert.Null(history.Find(3));
    }

    [Fact]
    public void Conversao_em_andamento_nao_sai_da_lista()
    {
        // Sem o registro, o resultado dela não teria onde aparecer.
        var history = With(1);

        for (long id = 2; id <= 102; id++)
        {
            history.Add(id, $"C:\\a\\{id}.jpg", Png, new Dictionary<string, string>());
            history.Complete(id, $"C:\\a\\{id}.png");
        }

        Assert.NotNull(history.Find(1));
        Assert.Equal(100, history.Items.Count);
    }

    [Fact]
    public void Item_com_cartao_na_tela_nao_sai_da_lista()
    {
        // Um cartão de erro antigo precisa continuar com os botões funcionando.
        var history = new ConversionHistory { InUse = id => id == 1 };
        history.Add(1, "C:\\a\\1.jpg", Png, new Dictionary<string, string>());
        history.Fail(1, "motivo");

        for (long id = 2; id <= 102; id++)
        {
            history.Add(id, $"C:\\a\\{id}.jpg", Png, new Dictionary<string, string>());
            history.Complete(id, $"C:\\a\\{id}.png");
        }

        Assert.NotNull(history.Find(1));
    }

    [Fact]
    public void Item_liberado_sai_na_proxima_conversao()
    {
        var inUse = true;
        var history = new ConversionHistory { InUse = _ => inUse };
        for (long id = 1; id <= 103; id++)
        {
            history.Add(id, $"C:\\a\\{id}.jpg", Png, new Dictionary<string, string>());
            history.Complete(id, $"C:\\a\\{id}.png");
        }

        Assert.Equal(103, history.Items.Count);

        inUse = false;
        history.Add(104, "C:\\a\\104.jpg", Png, new Dictionary<string, string>());
        history.Complete(104, "C:\\a\\104.png");

        Assert.Equal(100, history.Items.Count);
    }

    [Fact]
    public void Comeca_em_andamento_com_o_nome_da_origem()
    {
        var entry = With(1).Find(1)!;

        Assert.Equal(HistoryState.Running, entry.State);
        Assert.Equal("1.jpg", entry.Name);
    }

    [Fact]
    public void Concluida_passa_a_mostrar_o_arquivo_gerado()
    {
        var history = With(1);

        history.Complete(1, "C:\\a\\1.png");

        var entry = history.Find(1)!;
        Assert.Equal(HistoryState.Done, entry.State);
        Assert.Equal("1.png", entry.Name);
        Assert.Equal("C:\\a\\1.png", entry.OutputPath);
    }

    [Fact]
    public void Erro_guarda_o_motivo()
    {
        var history = With(1);

        history.Fail(1, "O arquivo está corrompido");

        var entry = history.Find(1)!;
        Assert.Equal(HistoryState.Failed, entry.State);
        Assert.Equal("O arquivo está corrompido", entry.Message);
    }

    [Fact]
    public void Cancelada_sai_da_lista()
    {
        var history = With(2);

        history.Remove(1);

        Assert.Equal([2L], history.Items.Select(entry => entry.Id));
    }

    [Fact]
    public void Item_que_ja_saiu_da_lista_nao_quebra()
    {
        var history = With(1);
        history.Remove(1);

        history.Complete(1, "C:\\a\\1.png");
        history.Fail(1, "motivo");

        Assert.Empty(history.Items);
    }
}
