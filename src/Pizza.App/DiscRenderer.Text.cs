using Pizza.Core.Visual;
using static Pizza.App.Interop.GdiPlus;

namespace Pizza.App;

/// <summary>Texto do menu: reto e seguindo o arco da fatia (prancha Ref-Texto-Fatias, versão B).</summary>
internal static unsafe partial class DiscRenderer
{
    /// <summary>Mede o texto letra a letra e desenha cada uma girada sobre o arco, com o meio no meio da fatia.</summary>
    private static void CurvedLabel(nint g, string text, nint family, float size, int style, uint color, double angle, double radius)
    {
        GdipCreateFont(family, size, style, UnitPixel, out var font);
        GdipStringFormatGetGenericTypographic(out var typographic);
        GdipCloneStringFormat(typographic, out var format);
        GdipSetStringFormatFlags(format, NoWrap | MeasureTrailingSpaces);
        GdipSetStringFormatAlign(format, AlignCenter);
        GdipSetStringFormatLineAlign(format, AlignCenter);

        // Largura de cada começo do texto: o centro de cada letra fica entre duas medidas seguidas.
        var room = new RectF { Width = 1000, Height = 100 };
        var edges = new double[text.Length + 1];
        for (var i = 1; i <= text.Length; i++)
        {
            RectF bounds;
            GdipMeasureString(g, text, i, font, &room, format, &bounds, null, null);
            edges[i] = bounds.Width;
        }

        var centers = new double[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            centers[i] = (edges[i] + edges[i + 1]) / 2;
        }

        var glyphs = CurvedText.Place(angle, radius, centers, edges[text.Length]);
        var scale = (float)ScaleOf();
        GdipCreateSolidFill(color, out var brush);
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                continue;
            }

            GdipResetWorldTransform(g);
            GdipScaleWorldTransform(g, scale, scale, 0);
            GdipTranslateWorldTransform(g, (float)(DiscLayout.CenterX + glyphs[i].X), (float)(DiscLayout.CenterY + glyphs[i].Y), 0);
            GdipRotateWorldTransform(g, (float)glyphs[i].Rotation, 0);
            var cell = new RectF { X = -size, Y = -size, Width = 2 * size, Height = 2 * size };
            GdipDrawString(g, text.Substring(i, 1), 1, font, &cell, format, brush);
        }

        GdipResetWorldTransform(g);
        GdipScaleWorldTransform(g, scale, scale, 0);
        GdipDeleteBrush(brush);
        GdipDeleteStringFormat(format);
        GdipDeleteFont(font);
    }

    private static void Text(nint g, string text, nint family, float size, int style, uint color, float x, float y, float width, float height)
    {
        GdipCreateFont(family, size, style, UnitPixel, out var font);
        GdipCreateStringFormat(NoWrap, 0, out var format);
        GdipSetStringFormatAlign(format, AlignCenter);
        GdipSetStringFormatLineAlign(format, AlignCenter);
        GdipSetStringFormatTrimming(format, TrimmingEllipsis);
        GdipCreateSolidFill(color, out var brush);
        var layout = new RectF { X = x, Y = y, Width = width, Height = height };
        GdipDrawString(g, text, -1, font, &layout, format, brush);
        GdipDeleteBrush(brush);
        GdipDeleteStringFormat(format);
        GdipDeleteFont(font);
    }

    private static nint Family(string name) =>
        GdipCreateFontFamilyFromName(name, 0, out var family) == 0 ? family : Family("Segoe UI");
}
