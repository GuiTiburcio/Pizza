using Pizza.Core.Notifications;
using static Pizza.App.Interop.GdiPlus;

namespace Pizza.App;

/// <summary>Menu da bandeja (ADR-020, prancha 4d-2): o vidro dos avisos, com a bolha B no item em foco.</summary>
internal static unsafe partial class DiscRenderer
{
    public static void ShowTrayMenu(nint hwnd, int left, int top, int width, int height, int dpi, TrayMenuState state, bool dark) =>
        ShowLayered(hwnd, left, top, width, height, dpi, dark, 1, (g, w, h) => DrawTrayMenu(g, state, w, h));

    private static void DrawTrayMenu(nint g, TrayMenuState state, float width, float height)
    {
        DrawGlass(g, width, height);

        // "Pizza · ativo": o ponto verde acompanha a palavra, a cor nunca é a única pista.
        const float header = TrayMenuState.HeaderTop;
        var dot = Ellipse(TrayMenuState.Padding + 16.5f, header + (TrayMenuState.HeaderHeight / 2f), 4.5f);
        Fill(g, Argb(255, 22, 163, 127), dot);
        GdipDeletePath(dot);
        Text(g, "Pizza", _semibold, 14, StyleBold, _palette.Ink, TrayMenuState.Padding + 30, header, 60, TrayMenuState.HeaderHeight, AlignNear);
        Text(g, "ativo", _regular, 13, StyleRegular, _palette.SliceInk, TrayMenuState.Padding + 76, header, 100, TrayMenuState.HeaderHeight, AlignNear);

        DrawTraySeparator(g, header + TrayMenuState.HeaderHeight, width);
        DrawTraySeparator(g, state.SecondSeparatorTop, width);

        for (var index = 0; index < state.Items.Count; index++)
        {
            var item = state.Items[index];
            var bounds = item.Bounds;
            if (index == state.Focus)
            {
                var shape = RoundedRect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, CardButtonRadius);
                Lens(g, shape, (bounds.Left + bounds.Right) / 2f, (bounds.Top + bounds.Bottom) / 2f, 0, 1);
            }

            DrawTrayIcon(g, item.Command, bounds.Left + 12, bounds.Top + 12, _palette.SliceInk);
            Text(g, item.Label, _regular, 14, StyleRegular, _palette.Ink, bounds.Left + 44, bounds.Top, bounds.Right - bounds.Left - 52, bounds.Bottom - bounds.Top, AlignNear);
        }
    }

    private static void DrawTraySeparator(nint g, float top, float width)
    {
        var line = RoundedRect(TrayMenuState.Padding + 10, top + (TrayMenuState.SeparatorHeight / 2f), width - (2 * (TrayMenuState.Padding + 10)), 1, 0.1f);
        Fill(g, WithAlpha(_palette.Ink, 0.12), line);
        GdipDeletePath(line);
    }

    /// <summary>Ícones de traço de 20 px: pasta (Arquivos), pasta com linhas (logs) e porta com seta (Sair).</summary>
    private static void DrawTrayIcon(nint g, TrayCommand command, float x, float y, uint ink)
    {
        GdipCreatePen1(ink, 1.5f, UnitWorld, out var pen);
        GdipSetPenLineJoin(pen, LineJoinRound);
        GdipSetPenStartCap(pen, LineCapRound);
        GdipSetPenEndCap(pen, LineCapRound);
        if (command == TrayCommand.Quit)
        {
            GdipDrawLine(g, pen, x + 8, y + 4.5f, x + 4.5f, y + 4.5f);
            GdipDrawLine(g, pen, x + 4.5f, y + 4.5f, x + 4.5f, y + 15.5f);
            GdipDrawLine(g, pen, x + 4.5f, y + 15.5f, x + 8, y + 15.5f);
            GdipDrawLine(g, pen, x + 8.5f, y + 10, x + 16.5f, y + 10);
            GdipDrawLine(g, pen, x + 13.5f, y + 7, x + 16.5f, y + 10);
            GdipDrawLine(g, pen, x + 16.5f, y + 10, x + 13.5f, y + 13);
        }
        else
        {
            GdipCreatePath(0, out var folder);
            GdipAddPathLine(folder, x + 3.5f, y + 6, x + 8.5f, y + 6);
            GdipAddPathLine(folder, x + 8.5f, y + 6, x + 10, y + 7.5f);
            GdipAddPathLine(folder, x + 10, y + 7.5f, x + 16.5f, y + 7.5f);
            GdipAddPathLine(folder, x + 16.5f, y + 7.5f, x + 16.5f, y + 16);
            GdipAddPathLine(folder, x + 16.5f, y + 16, x + 3.5f, y + 16);
            GdipClosePathFigure(folder);
            GdipDrawPath(g, pen, folder);
            GdipDeletePath(folder);
            if (command == TrayCommand.OpenLogs)
            {
                GdipDrawLine(g, pen, x + 7, y + 11, x + 13, y + 11);
                GdipDrawLine(g, pen, x + 7, y + 13.5f, x + 11, y + 13.5f);
            }
        }

        GdipDeletePen(pen);
    }
}
