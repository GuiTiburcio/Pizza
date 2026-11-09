using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

public class DragGestureDetectorTests
{
    private readonly DragGestureDetector _detector = new(thresholdX: 4, thresholdY: 4);
    private int _ctrlReads;

    private bool CtrlDown()
    {
        _ctrlReads++;
        return true;
    }

    private bool CtrlUp()
    {
        _ctrlReads++;
        return false;
    }

    [Fact]
    public void Ctrl_e_arraste_alem_do_limiar_inicia_o_gesto()
    {
        _detector.OnButtonDown(100, 100);

        Assert.Equal(GestureEvent.Started, _detector.OnMove(110, 100, CtrlDown));
        Assert.True(_detector.IsActive);
    }

    [Fact]
    public void Movimento_dentro_do_limiar_nao_inicia_nem_le_o_Ctrl()
    {
        _detector.OnButtonDown(100, 100);

        Assert.Equal(GestureEvent.None, _detector.OnMove(104, 104, CtrlDown));
        Assert.Equal(0, _ctrlReads);
    }

    [Fact]
    public void Arraste_sem_ctrl_nao_inicia()
    {
        _detector.OnButtonDown(100, 100);

        Assert.Equal(GestureEvent.None, _detector.OnMove(150, 150, CtrlUp));
    }

    [Fact]
    public void Movimento_sem_botao_pressionado_nao_le_o_Ctrl()
    {
        Assert.Equal(GestureEvent.None, _detector.OnMove(150, 150, CtrlDown));
        Assert.Equal(0, _ctrlReads);
    }

    [Fact]
    public void Ctrl_e_lido_uma_unica_vez_por_arraste()
    {
        // SEC-02: só no início do arraste.
        _detector.OnButtonDown(100, 100);
        _detector.OnMove(150, 150, CtrlUp);
        _detector.OnMove(160, 150, CtrlDown);
        _detector.OnMove(170, 150, CtrlDown);

        Assert.Equal(1, _ctrlReads);
    }

    [Fact]
    public void Ctrl_apertado_no_meio_do_arraste_nao_inicia()
    {
        _detector.OnButtonDown(100, 100);
        _detector.OnMove(150, 150, CtrlUp);

        Assert.Equal(GestureEvent.None, _detector.OnMove(160, 150, CtrlDown));
    }

    [Fact]
    public void Started_so_e_emitido_uma_vez_por_arraste()
    {
        _detector.OnButtonDown(100, 100);
        _detector.OnMove(150, 150, CtrlDown);

        Assert.Equal(GestureEvent.None, _detector.OnMove(200, 200, CtrlDown));
    }

    [Fact]
    public void Soltar_com_gesto_ativo_emite_Released()
    {
        _detector.OnButtonDown(100, 100);
        _detector.OnMove(150, 150, CtrlDown);

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
    public void Um_novo_arraste_le_o_Ctrl_de_novo()
    {
        _detector.OnButtonDown(100, 100);
        _detector.OnMove(150, 150, CtrlUp);
        _detector.OnButtonUp();

        _detector.OnButtonDown(300, 300);

        Assert.Equal(GestureEvent.Started, _detector.OnMove(320, 300, CtrlDown));
    }
}
