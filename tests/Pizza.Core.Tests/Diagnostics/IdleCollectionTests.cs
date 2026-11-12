using Pizza.Core.Diagnostics;

namespace Pizza.Core.Tests.Diagnostics;

/// <summary>NFR-01: depois de um tempo sem mensagens, o residente devolve a memória do coletor uma vez.</summary>
public class IdleCollectionTests
{
    [Fact]
    public void Mensagem_arma_e_o_disparo_coleta()
    {
        var idle = new IdleCollection();

        Assert.Equal(IdleStep.Rearm, idle.OnMessage(idleTimer: false));
        Assert.Equal(IdleStep.Collect, idle.OnMessage(idleTimer: true));
    }

    [Fact]
    public void Atividade_adia_a_coleta()
    {
        var idle = new IdleCollection();

        // Cada mensagem (quadros, avisos, cliques) volta a contar o tempo; um só disparo coleta.
        Assert.Equal(IdleStep.Rearm, idle.OnMessage(idleTimer: false));
        Assert.Equal(IdleStep.Rearm, idle.OnMessage(idleTimer: false));
        Assert.Equal(IdleStep.Collect, idle.OnMessage(idleTimer: true));
    }

    [Fact]
    public void Sem_atividade_nova_nao_coleta_de_novo()
    {
        var idle = new IdleCollection();
        idle.OnMessage(idleTimer: false);
        idle.OnMessage(idleTimer: true);

        Assert.Equal(IdleStep.None, idle.OnMessage(idleTimer: true));
    }

    [Fact]
    public void Disparo_sem_atividade_nao_coleta()
    {
        Assert.Equal(IdleStep.None, new IdleCollection().OnMessage(idleTimer: true));
    }
}
