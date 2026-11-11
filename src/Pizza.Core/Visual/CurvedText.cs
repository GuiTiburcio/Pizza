namespace Pizza.Core.Visual;

/// <summary>Uma letra do rótulo curvo: centro relativo ao centro do disco e giro em graus, no sentido da tela.</summary>
public readonly record struct CurvedGlyph(double X, double Y, double Rotation);

/// <summary>
/// Nome e descrição da fatia seguindo o arco, como num rótulo de pizza (prancha Ref-Texto-Fatias, versão B).
/// Na metade de cima o texto corre no sentido horário; na de baixo, ao contrário, para nunca ficar de cabeça
/// para baixo. Em qualquer fatia, lê-se da esquerda para a direita, com o nome acima da descrição.
/// O lado é decidido pela posição de repouso da fatia (<c>restDegrees</c>), para o texto não trocar de lado
/// enquanto as fatias giram na troca de aba.
/// </summary>
public static class CurvedText
{
    // Linha central do texto, sem hover. Na metade de cima, "acima" é para fora do disco; na de baixo, para o miolo.
    private const double TopName = 143;
    private const double TopCaption = 121;
    private const double BottomName = 120;
    private const double BottomCaption = 142;

    public static bool IsTopHalf(double centerDegrees) => Math.Sin(centerDegrees * Math.PI / 180) < 0;

    public static double NameRadius(double centerDegrees, double hover, double? restDegrees = null) =>
        WithHover(IsTopHalf(restDegrees ?? centerDegrees) ? TopName : BottomName, hover);

    public static double CaptionRadius(double centerDegrees, double hover, double? restDegrees = null) =>
        WithHover(IsTopHalf(restDegrees ?? centerDegrees) ? TopCaption : BottomCaption, hover);

    /// <summary>
    /// Posição de cada letra. <paramref name="charCenters"/> é o centro de cada letra medido ao longo da linha
    /// reta do texto, e <paramref name="totalWidth"/> a largura dessa linha; o meio do texto fica no meio da fatia.
    /// </summary>
    public static CurvedGlyph[] Place(double centerDegrees, double radius, IReadOnlyList<double> charCenters, double totalWidth, double? restDegrees = null)
    {
        var top = IsTopHalf(restDegrees ?? centerDegrees);
        var glyphs = new CurvedGlyph[charCenters.Count];
        for (var i = 0; i < glyphs.Length; i++)
        {
            var along = (charCenters[i] - (totalWidth / 2)) / radius * 180 / Math.PI;
            var degrees = top ? centerDegrees + along : centerDegrees - along;
            var radians = degrees * Math.PI / 180;
            glyphs[i] = new CurvedGlyph(radius * Math.Cos(radians), radius * Math.Sin(radians), top ? degrees + 90 : degrees - 90);
        }

        return glyphs;
    }

    // WF-2: no hover a fatia cresce 6% a partir do centro do disco e sai 5 px; o texto vai junto.
    private static double WithHover(double radius, double hover) =>
        (radius * (1 + (SliceGeometry.HoverScale * hover))) + (SliceGeometry.HoverShift * hover);
}
