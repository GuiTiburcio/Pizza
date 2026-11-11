using static Pizza.App.Interop.GdiPlus;

namespace Pizza.App;

/// <summary>
/// Ícones de traço da barra B (ADR-026, prancha Ref-Barra-Abas), num quadro de 18 px: Imagem, PDF (folha com a faixa
/// cheia embaixo), Docs (folha com linhas) e Áudio.
/// </summary>
internal static unsafe partial class DiscRenderer
{
    private static void DrawTabIcon(nint g, int tab, float x, float y, uint ink)
    {
        GdipCreatePen1(ink, 1.5f, UnitWorld, out var pen);
        GdipSetPenLineJoin(pen, LineJoinRound);
        GdipSetPenStartCap(pen, LineCapRound);
        GdipSetPenEndCap(pen, LineCapRound);
        switch (tab)
        {
            case 0:
                var frame = RoundedRect(x + 1.5f, y + 2.5f, 15, 13, 2);
                GdipDrawPath(g, pen, frame);
                GdipDeletePath(frame);
                GdipDrawLine(g, pen, x + 4, y + 13, x + 8, y + 8.5f);
                GdipDrawLine(g, pen, x + 8, y + 8.5f, x + 11, y + 11.5f);
                GdipDrawLine(g, pen, x + 11, y + 11.5f, x + 12.5f, y + 10);
                GdipDrawLine(g, pen, x + 12.5f, y + 10, x + 14, y + 11.5f);
                var sun = Ellipse(x + 12, y + 6.5f, 1.4f);
                Fill(g, ink, sun);
                GdipDeletePath(sun);
                break;

            case 1:
                DrawSheet(g, pen, x, y);
                var band = RoundedRect(x + 3.5f, y + 11, 11, 4, 0.5f);
                Fill(g, ink, band);
                GdipDeletePath(band);
                break;

            case 2:
                DrawSheet(g, pen, x, y);
                foreach (var line in new[] { 8f, 11f, 14f })
                {
                    GdipDrawLine(g, pen, x + 6, y + line, x + 12, y + line);
                }

                break;

            default:
                GdipDrawLine(g, pen, x + 7, y + 13.5f, x + 7, y + 4);
                GdipDrawLine(g, pen, x + 15, y + 12, x + 15, y + 2.5f);
                GdipDrawLine(g, pen, x + 7, y + 4, x + 15, y + 2.5f);
                foreach (var (cx, cy) in new[] { (x + 5f, y + 13.5f), (x + 13f, y + 12f) })
                {
                    var head = Ellipse(cx, cy, 2);
                    GdipDrawPath(g, pen, head);
                    GdipDeletePath(head);
                }

                break;
        }

        GdipDeletePen(pen);
    }

    /// <summary>Folha com o canto dobrado, base dos ícones de PDF e Docs.</summary>
    private static void DrawSheet(nint g, nint pen, float x, float y)
    {
        GdipCreatePath(0, out var sheet);
        GdipAddPathLine(sheet, x + 3.5f, y + 1, x + 11, y + 1);
        GdipAddPathLine(sheet, x + 11, y + 1, x + 14.5f, y + 4.5f);
        GdipAddPathLine(sheet, x + 14.5f, y + 4.5f, x + 14.5f, y + 17);
        GdipAddPathLine(sheet, x + 14.5f, y + 17, x + 3.5f, y + 17);
        GdipClosePathFigure(sheet);
        GdipDrawPath(g, pen, sheet);
        GdipDeletePath(sheet);
        GdipDrawLine(g, pen, x + 11, y + 1, x + 11, y + 4.5f);
        GdipDrawLine(g, pen, x + 11, y + 4.5f, x + 14.5f, y + 4.5f);
    }
}
