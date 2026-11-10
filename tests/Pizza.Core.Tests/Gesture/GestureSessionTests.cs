using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

public class GestureSessionTests
{
    private readonly GestureSession _session = new();

    [Fact]
    public void Soltar_fora_de_uma_fatia_deixa_o_menu_para_escolher_com_clique()
    {
        // ADR-025: soltar em qualquer lugar e escolher o formato com um clique.
        _session.Begin();
        _session.OnDragEnter(hasFiles: true);
        _session.OnButtonReleased();

        Assert.Equal(SessionAction.Pick, _session.OnReleaseSettled());
        Assert.Equal(GestureState.Picking, _session.State);
    }

    [Fact]
    public void Saida_do_arraste_depois_de_soltar_nao_cancela()
    {
        // Ao soltar fora, o Windows também avisa que o arraste saiu da nossa janela.
        _session.Begin();
        _session.OnDragEnter(hasFiles: true);
        _session.OnButtonReleased();
        _session.OnDragLeave();

        Assert.Equal(SessionAction.None, _session.OnLeaveTimeout());
        Assert.Equal(SessionAction.Pick, _session.OnReleaseSettled());
    }

    [Fact]
    public void Esc_durante_o_arraste_continua_cancelando()
    {
        // Esc com o botão ainda apertado: o arraste sai sem o botão ter subido.
        _session.Begin();
        _session.OnDragEnter(hasFiles: true);
        _session.OnDragLeave();

        Assert.Equal(SessionAction.Close, _session.OnLeaveTimeout());
        Assert.Equal(GestureState.Idle, _session.State);
    }

    [Fact]
    public void Soltar_antes_de_o_menu_abrir_cancela()
    {
        _session.Begin();
        _session.OnButtonReleased();

        Assert.Equal(SessionAction.Close, _session.OnReleaseSettled());
        Assert.Equal(GestureState.Idle, _session.State);
    }

    [Fact]
    public void Escolhendo_ignora_eventos_do_arraste_que_terminou()
    {
        _session.Begin();
        _session.OnDragEnter(hasFiles: true);
        _session.OnButtonReleased();
        _session.OnReleaseSettled();

        Assert.Equal(SessionAction.None, _session.OnDragLeave());
        Assert.Equal(SessionAction.None, _session.OnDragEnter(hasFiles: true));
        Assert.Equal(GestureState.Picking, _session.State);
    }

    [Fact]
    public void Comecar_o_gesto_nao_mostra_o_menu()
    {
        _session.Begin();

        Assert.Equal(GestureState.WaitingForFile, _session.State);
    }

    [Fact]
    public void Arraste_de_arquivo_abre_o_menu()
    {
        _session.Begin();

        Assert.Equal(SessionAction.ShowMenu, _session.OnDragEnter(hasFiles: true));
        Assert.Equal(GestureState.Menu, _session.State);
    }

    [Fact]
    public void Arraste_que_nao_e_de_arquivo_nunca_abre_o_menu()
    {
        // SEC-06: texto e URL são ignorados.
        _session.Begin();

        Assert.Equal(SessionAction.Close, _session.OnDragEnter(hasFiles: false));
        Assert.Equal(GestureState.Idle, _session.State);
    }

    [Fact]
    public void Entrar_de_novo_com_o_menu_aberto_nao_faz_nada()
    {
        _session.Begin();
        _session.OnDragEnter(hasFiles: true);

        Assert.Equal(SessionAction.None, _session.OnDragEnter(hasFiles: true));
    }

    [Fact]
    public void Evento_sem_gesto_ativo_e_ignorado()
    {
        Assert.Equal(SessionAction.None, _session.OnDragEnter(hasFiles: true));
        Assert.Equal(SessionAction.None, _session.OnDragLeave());
    }

    [Fact]
    public void Sair_pede_a_espera_para_distinguir_troca_de_janela_de_Esc()
    {
        _session.Begin();
        _session.OnDragEnter(hasFiles: true);

        Assert.Equal(SessionAction.WaitForReentry, _session.OnDragLeave());
    }

    [Fact]
    public void Voltar_a_entrar_durante_a_espera_mantem_o_gesto()
    {
        // O cursor passou da camada para o disco: DragLeave seguido de DragEnter.
        _session.Begin();
        _session.OnDragEnter(hasFiles: true);
        _session.OnDragLeave();
        _session.OnDragEnter(hasFiles: true);

        Assert.Equal(SessionAction.None, _session.OnLeaveTimeout());
        Assert.Equal(GestureState.Menu, _session.State);
    }

    [Fact]
    public void Sem_reentrada_o_arraste_foi_cancelado()
    {
        // SEC-03: o Esc chega pelo próprio OLE, como DragLeave.
        _session.Begin();
        _session.OnDragEnter(hasFiles: true);
        _session.OnDragLeave();

        Assert.Equal(SessionAction.Close, _session.OnLeaveTimeout());
        Assert.Equal(GestureState.Idle, _session.State);
    }

    [Fact]
    public void Soltar_antes_da_confirmacao_mantem_a_camada_durante_a_espera()
    {
        // Se a camada saísse agora, o drop cairia na pasta de baixo e viraria cópia.
        _session.Begin();

        Assert.Equal(SessionAction.WaitForDrop, _session.OnButtonReleased());
        Assert.Equal(GestureState.WaitingForFile, _session.State);
    }

    [Fact]
    public void Soltar_com_o_menu_aberto_espera_o_drop()
    {
        _session.Begin();
        _session.OnDragEnter(hasFiles: true);

        Assert.Equal(SessionAction.WaitForDrop, _session.OnButtonReleased());
    }

    [Fact]
    public void Confirmacao_que_chega_depois_de_soltar_nao_abre_o_menu()
    {
        _session.Begin();
        _session.OnButtonReleased();

        Assert.Equal(SessionAction.None, _session.OnDragEnter(hasFiles: true));
        Assert.Equal(GestureState.WaitingForFile, _session.State);
    }

    [Fact]
    public void Soltar_sem_gesto_nao_faz_nada()
    {
        Assert.Equal(SessionAction.None, _session.OnButtonReleased());
    }

    [Fact]
    public void Um_novo_gesto_depois_de_soltar_volta_a_abrir_o_menu()
    {
        _session.Begin();
        _session.OnButtonReleased();
        _session.End();
        _session.Begin();

        Assert.Equal(SessionAction.ShowMenu, _session.OnDragEnter(hasFiles: true));
    }

    [Fact]
    public void Terminar_volta_ao_repouso()
    {
        _session.Begin();
        _session.OnDragEnter(hasFiles: true);
        _session.End();

        Assert.Equal(GestureState.Idle, _session.State);
    }
}
