namespace Pizza.Core.Conversions;

/// <summary>Rolagem do painel em pixels lógicos, sem reduzir fonte ou alvos em telas pequenas.</summary>
public sealed class PanelViewport
{
    public const int FooterHeight = 68;
    private readonly int _fullHeight;

    public PanelViewport(int fullHeight, int availableHeight)
    {
        _fullHeight = fullHeight;
        Height = Math.Min(fullHeight, availableHeight);
    }

    public int Height { get; }

    public bool CanScroll => Height < _fullHeight;

    public int ContentHeight => CanScroll ? Height - FooterHeight : Height;

    public int Offset { get; private set; }

    public void Scroll(int distance) => Offset = Math.Clamp(Offset + distance, 0, _fullHeight - ContentHeight);

    public void Reveal(PanelRect bounds)
    {
        if (bounds.Top < Offset)
        {
            Scroll(bounds.Top - Offset);
        }
        else if (bounds.Bottom > Offset + ContentHeight)
        {
            Scroll(bounds.Bottom - Offset - ContentHeight);
        }
    }
}
