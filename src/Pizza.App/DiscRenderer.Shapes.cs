using Pizza.Core.Visual;
using static Pizza.App.Interop.GdiPlus;

namespace Pizza.App;

/// <summary>Vidro e formas: contornos, preenchimentos, degradês e traços com GDI+ (DES-1, ADR-023).</summary>
internal static unsafe partial class DiscRenderer
{
    /// <summary>Vidro: o fundo desfocado recortado na forma, um véu branco e a borda clara (DES-1).</summary>
    private static void Glass(nint g, nint path, double fill, double border)
    {
        if (_backdrop != 0)
        {
            GdipSetClipPath(g, path, CombineReplace);
            GdipResetWorldTransform(g);
            GdipDrawImageRectI(g, _backdrop, 0, 0, _width, _height);
            GdipResetClip(g);
            GdipScaleWorldTransform(g, (float)ScaleOf(), (float)ScaleOf(), 0);
            Fill(g, WithAlpha(_palette.Veil, fill), path);
        }
        else
        {
            // Transparência desligada: sólido 92%, a mesma forma.
            Fill(g, WithAlpha(_palette == Dark ? Argb(255, 32, 36, 48) : Argb(255, 255, 255, 255), 0.92), path);
        }

        Stroke(g, WithAlpha(Argb(255, 255, 255, 255), border), 1, path);
        GdipDeletePath(path);
    }

    /// <summary>
    /// Bolha de vidro B (ADR-027, prancha 4b-2): sombra curta, o fundo um pouco ampliado, véu claro, brilho no alto,
    /// borda clara e franjas coloridas por dentro da borda. <paramref name="strength"/> esmaece a bolha inteira.
    /// </summary>
    private static void Lens(nint g, nint path, float cx, float cy, double swell, double strength, double? veil = null)
    {
        var magnify = veil is not null;
        // Sombra: só fora da bolha, um pouco abaixo.
        GdipSetClipPath(g, path, CombineExclude);
        foreach (var (dy, alpha) in new[] { (4f, 0.06), (2.5f, 0.09), (1f, 0.12) })
        {
            GdipTranslateWorldTransform(g, 0, dy, 0);
            Stroke(g, WithAlpha(Argb(255, 15, 20, 40), alpha * strength), 2, path);
            GdipTranslateWorldTransform(g, 0, -dy, 0);
        }

        GdipResetClip(g);

        if (magnify && _backdrop != 0)
        {
            // Lente: a mesma cópia da tela do vidro (ADR-023), ampliada em volta do centro da bolha.
            var scale = ScaleOf();
            var zoom = 1.06 + (0.04 * swell);
            var x = cx * scale;
            var y = cy * scale;
            GdipSetClipPath(g, path, CombineReplace);
            GdipResetWorldTransform(g);
            GdipDrawImageRectI(g, _backdrop, (int)Math.Round(x - (x * zoom)), (int)Math.Round(y - (y * zoom)), (int)Math.Round(_width * zoom), (int)Math.Round(_height * zoom));
            GdipScaleWorldTransform(g, (float)scale, (float)scale, 0);
            GdipResetClip(g);

            // O mesmo véu da superfície de baixo: sem ele, um fundo vivo ficaria cru na lente e o texto sumiria.
            Fill(g, WithAlpha(_palette.Veil, veil!.Value), path);
        }

        Fill(g, WithAlpha(Argb(255, 255, 255, 255), (_palette == Dark ? 0.14 : 0.42) * strength), path);

        GdipSetClipPath(g, path, CombineReplace);
        if (_fringes)
        {
            // Franjas: a borda deslocada para dentro, rosa à esquerda, ciano à direita e amarela no alto.
            foreach (var (dx, dy, color) in new[]
            {
                (2f, 0f, Argb(140, 255, 90, 170)),
                (-2f, 0f, Argb(153, 60, 200, 255)),
                (0f, 2f, Argb(140, 255, 230, 120)),
            })
            {
                GdipTranslateWorldTransform(g, dx, dy, 0);
                Stroke(g, WithAlpha(color, ((color >> 24) / 255.0) * strength), 3, path);
                GdipTranslateWorldTransform(g, -dx, -dy, 0);
            }
        }

        // Brilho no alto e reflexo claro embaixo, por dentro da borda.
        GdipTranslateWorldTransform(g, 0, 1.5f, 0);
        Stroke(g, WithAlpha(Argb(255, 255, 255, 255), 0.95 * strength), 1.5f, path);
        GdipTranslateWorldTransform(g, 0, -3.5f, 0);
        Stroke(g, WithAlpha(Argb(255, 255, 255, 255), 0.35 * strength), 2, path);
        GdipTranslateWorldTransform(g, 0, 2, 0);
        GdipResetClip(g);

        Stroke(g, WithAlpha(Argb(255, 255, 255, 255), 0.85 * strength), 1, path);
        GdipDeletePath(path);
    }

    private static nint SlicePath(SliceShape shape)
    {
        GdipCreatePath(0, out var path);
        Bezier(path, shape.OuterStart);
        ArcTo(path, shape.OuterArc);
        Bezier(path, shape.OuterEnd);
        GdipAddPathLine(path, (float)shape.OuterEnd.To.X, (float)shape.OuterEnd.To.Y, (float)shape.InnerEnd.From.X, (float)shape.InnerEnd.From.Y);
        Bezier(path, shape.InnerEnd);
        ArcTo(path, shape.InnerArc);
        Bezier(path, shape.InnerStart);
        GdipClosePathFigure(path);
        return path;
    }

    private static void Bezier(nint path, Corner corner) =>
        GdipAddPathBezier(path, (float)corner.From.X, (float)corner.From.Y, (float)corner.Control1.X, (float)corner.Control1.Y, (float)corner.Control2.X, (float)corner.Control2.Y, (float)corner.To.X, (float)corner.To.Y);

    private static void ArcTo(nint path, Pizza.Core.Visual.Arc arc) =>
        GdipAddPathArc(path, (float)(arc.CenterX - arc.Radius), (float)(arc.CenterY - arc.Radius), (float)(2 * arc.Radius), (float)(2 * arc.Radius), (float)arc.Start, (float)arc.Sweep);

    private static nint Ellipse(float cx, float cy, float radius)
    {
        GdipCreatePath(0, out var path);
        GdipAddPathEllipse(path, cx - radius, cy - radius, 2 * radius, 2 * radius);
        return path;
    }

    private static nint RoundedRect(float x, float y, float width, float height, float radius)
    {
        var d = 2 * radius;
        GdipCreatePath(0, out var path);
        GdipAddPathArc(path, x, y, d, d, 180, 90);
        GdipAddPathArc(path, x + width - d, y, d, d, 270, 90);
        GdipAddPathArc(path, x + width - d, y + height - d, d, d, 0, 90);
        GdipAddPathArc(path, x, y + height - d, d, d, 90, 90);
        GdipClosePathFigure(path);
        return path;
    }

    private static void Fill(nint g, uint color, nint path)
    {
        GdipCreateSolidFill(color, out var brush);
        GdipFillPath(g, brush, path);
        GdipDeleteBrush(brush);
    }

    private static void Gradient(nint g, nint path, float top, float bottom, uint from, uint to)
    {
        var start = new PointF { X = 0, Y = top - 0.5f };
        var end = new PointF { X = 0, Y = bottom + 0.5f };
        // Espelhado nas pontas: se sobrar algum pixel, ele repete a cor da borda em vez de saltar.
        GdipCreateLineBrush(&start, &end, from, to, WrapTileFlipXY, out var brush);
        GdipFillPath(g, brush, path);
        GdipDeleteBrush(brush);
    }

    private static void Stroke(nint g, uint color, float width, nint path)
    {
        GdipCreatePen1(color, width, UnitWorld, out var pen);
        GdipSetPenLineJoin(pen, LineJoinRound);
        GdipDrawPath(g, pen, path);
        GdipDeletePen(pen);
    }

    private static void Arc(nint g, uint color, float width, float cx, float cy, float radius, double start, double sweep, bool round = false)
    {
        GdipCreatePen1(color, width, UnitWorld, out var pen);
        if (round)
        {
            GdipSetPenStartCap(pen, LineCapRound);
            GdipSetPenEndCap(pen, LineCapRound);
        }

        GdipDrawArc(g, pen, cx - radius, cy - radius, 2 * radius, 2 * radius, (float)start, (float)sweep);
        GdipDeletePen(pen);
    }
}
