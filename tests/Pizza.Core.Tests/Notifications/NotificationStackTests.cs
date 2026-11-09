using Pizza.Core.Notifications;

namespace Pizza.Core.Tests.Notifications;

/// <summary>FR-40 a FR-42 e PRIV-18, com as emendas da SPECS §2.1.</summary>
public class NotificationStackTests
{
    private static NotificationStack WithSuccess(int count)
    {
        var stack = new NotificationStack();
        for (var id = 1; id <= count; id++)
        {
            stack.Set(id, NotificationKind.Success, "Conversão concluída", $"{id}.png");
        }

        return stack;
    }

    [Fact]
    public void Progresso_so_aparece_depois_de_1_segundo()
    {
        var stack = new NotificationStack();
        stack.Set(1, NotificationKind.Progress, "Convertendo…", "a.jpg");

        stack.Advance(999);
        Assert.Empty(stack.Visible);

        stack.Advance(1);
        Assert.Single(stack.Visible);
    }

    [Fact]
    public void Conversao_rapida_nunca_mostra_progresso()
    {
        var stack = new NotificationStack();
        stack.Set(1, NotificationKind.Progress, "Convertendo…", "a.jpg");

        stack.Advance(300);
        stack.Set(1, NotificationKind.Success, "Conversão concluída", "a.png");

        Assert.Equal(NotificationKind.Success, Assert.Single(stack.Visible).Kind);
    }

    [Fact]
    public void Informa_quando_ha_cartao_esperando_para_aparecer()
    {
        // O residente confere o "Não perturbe" no momento de revelar o cartão.
        var stack = new NotificationStack();
        stack.Set(1, NotificationKind.Progress, "Convertendo…", "a.jpg");
        Assert.True(stack.HasDelayed);

        stack.Advance(1000);

        Assert.False(stack.HasDelayed);
    }

    [Fact]
    public void Progresso_nao_expira()
    {
        var stack = new NotificationStack();
        stack.Set(1, NotificationKind.Progress, "Convertendo…", "a.jpg");

        stack.Advance(60_000);

        Assert.Single(stack.Visible);
    }

    [Fact]
    public void Sucesso_some_em_5_segundos()
    {
        var stack = WithSuccess(1);

        stack.Advance(4999);
        Assert.Single(stack.Visible);

        stack.Advance(1);
        Assert.Empty(stack.Visible);
    }

    [Fact]
    public void Mouse_em_cima_pausa_e_o_tempo_continua_ao_sair()
    {
        var stack = WithSuccess(1);
        stack.Advance(4000);

        stack.Paused = true;
        stack.Advance(60_000);
        Assert.Single(stack.Visible);

        stack.Paused = false;
        stack.Advance(999);
        Assert.Single(stack.Visible);
        stack.Advance(1);
        Assert.Empty(stack.Visible);
    }

    [Fact]
    public void Erro_fica_ate_fechar()
    {
        var stack = new NotificationStack();
        stack.Set(1, NotificationKind.Error, "Não foi possível converter", "a.jpg");

        stack.Advance(3_600_000);
        Assert.Single(stack.Visible);

        stack.Dismiss(1);
        Assert.Empty(stack.Visible);
    }

    [Fact]
    public void No_maximo_tres_visiveis_e_as_mais_novas_primeiro()
    {
        var stack = WithSuccess(5);

        Assert.Equal([5L, 4L, 3L], stack.Visible.Select(item => item.Id));
        Assert.Equal(2, stack.Overflow);
    }

    [Fact]
    public void Quando_uma_sai_a_seguinte_aparece()
    {
        var stack = WithSuccess(4);

        stack.Dismiss(4);

        Assert.Equal([3L, 2L, 1L], stack.Visible.Select(item => item.Id));
        Assert.Equal(0, stack.Overflow);
    }

    [Fact]
    public void Retida_nao_aparece_nem_perde_tempo_e_volta_depois()
    {
        // PRIV-18 e "Não perturbe".
        var stack = WithSuccess(1);

        stack.Held = true;
        stack.Advance(60_000);
        Assert.Empty(stack.Visible);
        Assert.Equal(0, stack.Overflow);

        stack.Held = false;
        Assert.Single(stack.Visible);
        stack.Advance(4999);
        Assert.Single(stack.Visible);
    }

    [Fact]
    public void Atualizar_o_cartao_nao_muda_a_posicao()
    {
        var stack = new NotificationStack();
        stack.Set(1, NotificationKind.Error, "Erro", "a");
        stack.Set(2, NotificationKind.Error, "Erro", "b");

        stack.Set(1, NotificationKind.Error, "Erro", "c");

        Assert.Equal([2L, 1L], stack.Visible.Select(item => item.Id));
        Assert.Equal("c", stack.Visible[1].Detail);
    }

    [Fact]
    public void Sem_nada_para_contar_o_relogio_para()
    {
        // NFR-02: em repouso, nenhum temporizador.
        var stack = new NotificationStack();
        Assert.False(stack.NeedsClock);

        stack.Set(1, NotificationKind.Error, "Erro", "a");
        Assert.False(stack.NeedsClock);

        stack.Set(2, NotificationKind.Success, "Pronto", "b");
        Assert.True(stack.NeedsClock);

        stack.Paused = true;
        Assert.False(stack.NeedsClock);
    }
}
