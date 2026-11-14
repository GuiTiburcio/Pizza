namespace Pizza.App;

/// <summary>
/// ADR-037: a barra de abas e a faixa de níveis ficam numa janela própria, que segue o disco e pode ser
/// arrastada. Ela tem o seu próprio quadro e a sua própria cópia desfocada da tela; para reaproveitar o desenho,
/// as duas superfícies trocam de lugar só durante a chamada.
/// </summary>
internal static unsafe partial class DiscRenderer
{
    private static GlassSurface _spare;
    private static bool _drawTop;

    public static void PrepareTop(int left, int top, int width, int height, int dpi, bool live = false) =>
        WithTop(() => Prepare(left, top, width, height, dpi, live));

    public static void ShowTop(nint hwnd, int left, int top, DiscFrame frame) =>
        WithTop(() =>
        {
            _drawTop = true;
            try
            {
                Show(hwnd, left, top, frame);
            }
            finally
            {
                _drawTop = false;
            }
        });

    /// <summary>Zera e solta a cópia da tela da barra (ADR-023).</summary>
    public static void ReleaseTop() => WithTop(Release);

    private static void WithTop(Action action)
    {
        Swap();
        try
        {
            action();
        }
        finally
        {
            Swap();
        }
    }

    private static void Swap()
    {
        var current = new GlassSurface(_memory, _target, _originalTarget, _bits, _backdropMemory, _backdropDib, _originalBackdrop, _backdropBits, _backdrop, _width, _height, _glassAlpha, _live);
        (_memory, _target, _originalTarget, _bits, _backdropMemory, _backdropDib, _originalBackdrop, _backdropBits, _backdrop, _width, _height, _glassAlpha, _live) =
            (_spare.Memory, _spare.Target, _spare.OriginalTarget, _spare.Bits, _spare.BackdropMemory, _spare.BackdropDib, _spare.OriginalBackdrop, _spare.BackdropBits, _spare.Backdrop, _spare.Width, _spare.Height, _spare.GlassAlpha, _spare.Live);
        _spare = current;
    }

    private readonly record struct GlassSurface(
        nint Memory, nint Target, nint OriginalTarget, nint Bits, nint BackdropMemory, nint BackdropDib, nint OriginalBackdrop,
        nint BackdropBits, nint Backdrop, int Width, int Height, double GlassAlpha, bool Live);
}
