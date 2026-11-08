namespace Pizza.Core.Gesture;

/// <summary>O que o detector concluiu a partir de um evento de mouse ou teclado.</summary>
public enum GestureEvent
{
    None,

    /// <summary>Ctrl + arraste passou do limiar: o menu deve abrir.</summary>
    Started,

    /// <summary>O botão foi solto com o gesto ativo.</summary>
    Released,

    /// <summary>Esc com o gesto ativo.</summary>
    Cancelled,
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
    private bool _cancelled;
    private int _startX;
    private int _startY;

    /// <summary>Verdadeiro entre <see cref="GestureEvent.Started"/> e o fim do gesto.</summary>
    public bool IsActive { get; private set; }

    public GestureEvent OnButtonDown(int x, int y)
    {
        _buttonDown = true;
        _cancelled = false;
        IsActive = false;
        _startX = x;
        _startY = y;
        return GestureEvent.None;
    }

    /// <summary>
    /// Movimento do mouse. O gesto começa quando o botão está pressionado, o cursor passou do limiar
    /// e o Ctrl está pressionado. Ctrl apertado no meio do arraste também abre, a menos que o
    /// usuário tenha cancelado com Esc neste mesmo arraste.
    /// </summary>
    public GestureEvent OnMove(int x, int y, bool ctrlDown)
    {
        if (!_buttonDown || IsActive || _cancelled || !ctrlDown)
        {
            return GestureEvent.None;
        }

        var passed = Math.Abs(x - _startX) > thresholdX || Math.Abs(y - _startY) > thresholdY;
        if (!passed)
        {
            return GestureEvent.None;
        }

        IsActive = true;
        return GestureEvent.Started;
    }

    public GestureEvent OnButtonUp()
    {
        _buttonDown = false;
        _cancelled = false;
        if (!IsActive)
        {
            return GestureEvent.None;
        }

        IsActive = false;
        return GestureEvent.Released;
    }

    public GestureEvent OnEscape()
    {
        if (!IsActive)
        {
            return GestureEvent.None;
        }

        IsActive = false;
        _cancelled = true;
        return GestureEvent.Cancelled;
    }
}
