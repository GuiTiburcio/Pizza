namespace Pizza.Core.Gesture;

/// <summary>O que o detector concluiu a partir de um evento de mouse.</summary>
public enum GestureEvent
{
    None,

    /// <summary>Ctrl + arraste passou do limiar: o gesto começou.</summary>
    Started,

    /// <summary>O botão foi solto com o gesto ativo.</summary>
    Released,
}

/// <summary>
/// Reconhece o gesto "segurar Ctrl e arrastar" a partir dos eventos crus do hook de mouse.
/// Não sabe se o arraste é de um arquivo: isso só o alvo de drop OLE descobre (Parte 6, estado 2).
/// </summary>
/// <param name="thresholdX">Distância horizontal mínima, em pixels, para contar como arraste (SM_CXDRAG).</param>
/// <param name="thresholdY">Distância vertical mínima, em pixels, para contar como arraste (SM_CYDRAG).</param>
public sealed class DragGestureDetector(int thresholdX, int thresholdY)
{
    private bool _buttonDown;
    private bool _decided;
    private int _startX;
    private int _startY;

    /// <summary>Verdadeiro entre <see cref="GestureEvent.Started"/> e a soltura do botão.</summary>
    public bool IsActive { get; private set; }

    public GestureEvent OnButtonDown(int x, int y)
    {
        _buttonDown = true;
        _decided = false;
        IsActive = false;
        _startX = x;
        _startY = y;
        return GestureEvent.None;
    }

    /// <summary>
    /// Movimento do mouse. Quando o cursor passa do limiar, decide uma única vez: o Ctrl é lido
    /// nesse instante e nunca mais neste arraste (SEC-02).
    /// </summary>
    public GestureEvent OnMove(int x, int y, Func<bool> isCtrlDown)
    {
        if (!_buttonDown || _decided)
        {
            return GestureEvent.None;
        }

        if (Math.Abs(x - _startX) <= thresholdX && Math.Abs(y - _startY) <= thresholdY)
        {
            return GestureEvent.None;
        }

        _decided = true;
        if (!isCtrlDown())
        {
            return GestureEvent.None;
        }

        IsActive = true;
        return GestureEvent.Started;
    }

    public GestureEvent OnButtonUp()
    {
        _buttonDown = false;
        if (!IsActive)
        {
            return GestureEvent.None;
        }

        IsActive = false;
        return GestureEvent.Released;
    }
}
