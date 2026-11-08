using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

public class DragGestureDetectorTests
{
    private readonly DragGestureDetector _detector = new(thresholdX: 4, thresholdY: 4);

    [Fact]
    public void Ctrl_e_arraste_alem_do_limiar_inicia_o_gesto()
    {
        _detector.OnButtonDown(100, 100);

        Assert.Equal(GestureEvent.Started, _detector.OnMove(110, 100, ctrlDown: true));
        Assert.True(_detector.IsActive);
    }

    [Fact]
    public void Movimento_dentro_do_limiar_nao_inicia()
    {
        _detector.OnButtonDown(100, 100);

        Assert.Equal(GestureEvent.None, _detector.OnMove(104, 104, ctrlDown: true));
        Assert.False(_detector.IsActive);
    }

    [Fact]
    public void Arraste_sem_ctrl_nao_inicia()
    {
        _detector.OnButtonDown(100, 100);

        Assert.Equal(GestureEvent.None, _detector.OnMove(150, 150, ctrlDown: false));
    }

    [Fact]
    public void Movimento_sem_botao_pressionado_nao_inicia()
    {
        Assert.Equal(GestureEvent.None, _detector.OnMove(150, 150, ctrlDown: true));
    }

    [Fact]
    public void Ctrl_apertado_no_meio_do_arraste_inicia()
    {
        _detector.OnButtonDown(100, 100);
        _detector.OnMove(150, 150, ctrlDown: false);

        Assert.Equal(GestureEvent.Started, _detector.OnMove(160, 150, ctrlDown: true));
    }

    [Fact]
    public void Started_so_e_emitido_uma_vez_por_arraste()
    {
        _detector.OnButtonDown(100, 100);
        _detector.OnMove(150, 150, ctrlDown: true);

        Assert.Equal(GestureEvent.None, _detector.OnMove(200, 200, ctrlDown: true));
    }

    [Fact]
    public void Soltar_com_gesto_ativo_emite_Released()
    {
        _detector.OnButtonDown(100, 100);
        _detector.OnMove(150, 150, ctrlDown: true);

        Assert.Equal(GestureEvent.Released, _detector.OnButtonUp());
        Assert.False(_detector.IsActive);
    }

    [Fact]
    public void Soltar_sem_gesto_nao_emite_nada()
    {
        _detector.OnButtonDown(100, 100);

        Assert.Equal(GestureEvent.None, _detector.OnButtonUp());
    }

    [Fact]
    public void Esc_cancela_e_o_mesmo_arraste_nao_reabre()
    {
        _detector.OnButtonDown(100, 100);
        _detector.OnMove(150, 150, ctrlDown: true);

        Assert.Equal(GestureEvent.Cancelled, _detector.OnEscape());
        Assert.Equal(GestureEvent.None, _detector.OnMove(200, 200, ctrlDown: true));
        Assert.Equal(GestureEvent.None, _detector.OnButtonUp());
    }

    [Fact]
    public void Depois_de_cancelar_um_novo_arraste_funciona()
    {
        _detector.OnButtonDown(100, 100);
        _detector.OnMove(150, 150, ctrlDown: true);
        _detector.OnEscape();
        _detector.OnButtonUp();

        _detector.OnButtonDown(300, 300);
        Assert.Equal(GestureEvent.Started, _detector.OnMove(320, 300, ctrlDown: true));
    }

    [Fact]
    public void Esc_sem_gesto_ativo_nao_emite_nada()
    {
        Assert.Equal(GestureEvent.None, _detector.OnEscape());
    }
}
