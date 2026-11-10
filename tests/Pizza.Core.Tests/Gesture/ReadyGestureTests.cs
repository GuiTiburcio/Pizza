using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

/// <summary>ADR-025 / TST-01: a soltura fora de uma fatia prepara a escolha por clique.</summary>
public class ReadyGestureTests
{
    private static GestureSession Ready()
    {
        var session = new GestureSession();
        session.Begin();
        session.OnDragEnter(true);
        Assert.Equal(SessionAction.PrepareChoice, session.OnDrop(true));
        Assert.Equal(GestureState.Choosing, session.State);
        return session;
    }

    [Fact]
    public void Soltura_atrasada_nao_fecha_a_escolha() => Assert.Equal(SessionAction.None, Ready().OnButtonReleased());

    [Fact]
    public void DragLeave_atrasado_nao_fecha_a_escolha() => Assert.Equal(SessionAction.None, Ready().OnDragLeave());

    [Fact]
    public void Timeout_atrasado_nao_fecha_a_escolha() => Assert.Equal(SessionAction.None, Ready().OnDropTimeout());

    [Fact]
    public void Timeout_sem_drop_confirmado_fecha_o_gesto()
    {
        var session = new GestureSession();
        session.Begin();
        Assert.Equal(SessionAction.Close, session.OnDropTimeout());
        Assert.Equal(GestureState.Idle, session.State);
    }

    [Fact]
    public void Dados_invalidos_na_soltura_nao_preparam_escolha()
    {
        var session = new GestureSession();
        session.Begin();
        session.OnDragEnter(true);
        Assert.Equal(SessionAction.Close, session.OnDrop(false));
        Assert.Equal(GestureState.Idle, session.State);
    }

    [Fact]
    public void Drop_sem_menu_nao_prepara_escolha()
    {
        var session = new GestureSession();
        Assert.Equal(SessionAction.None, session.OnDrop(true));
        session.Begin();
        Assert.Equal(SessionAction.Close, session.OnDrop(true));
    }

    [Fact]
    public void Cancelar_e_recomecar_limpa_a_escolha()
    {
        var session = Ready();
        session.End();
        session.Begin();
        Assert.Equal(SessionAction.ShowMenu, session.OnDragEnter(true));
    }
}
