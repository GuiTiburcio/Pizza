using Pizza.Core.Conversions;
using Pizza.Core.Notifications;
using static Pizza.App.Interop.GdiPlus;

namespace Pizza.App;

/// <summary>
/// Janela Arquivos (ADR-028, prancha 4c-3): o mesmo vidro translúcido dos avisos, com linhas compactas. Só a linha em
/// foco abre e mostra os botões; a bolha de vidro B marca o botão em foco.
/// </summary>
internal static unsafe partial class DiscRenderer
{
    public static void ShowFiles(nint hwnd, int left, int top, int width, int height, int dpi, FilesPanelState state, IReadOnlyList<HistoryEntry> items, bool dark, double fade) =>
        ShowLayered(hwnd, left, top, width, height, dpi, dark, fade, (g, w, h) => DrawFiles(g, state, items, w, h));

    private static void DrawFiles(nint g, FilesPanelState state, IReadOnlyList<HistoryEntry> items, float width, float height)
    {
        DrawGlass(g, width, height);
        const float header = FilesPanelState.HeaderHeight;
        Text(g, "Arquivos", _semibold, 14, StyleBold, _palette.Ink, 20, 4, width - 40, header - 4, AlignNear);
        if (items.Count == 0)
        {
            Text(g, "Nenhuma conversão ainda", _regular, 13, StyleRegular, _palette.SliceInk, 0, header, width, FilesPanelState.EmptyHeight);
            return;
        }

        // As linhas que sobem com a rolagem somem sob o cabeçalho.
        GdipSetClipRect(g, 0, header, width, state.ViewportHeight, CombineReplace);
        for (var row = 0; row < items.Count; row++)
        {
            var top = header + state.RowTop(row) - state.Scroll;
            if (top + state.RowHeightAt(row) > header && top < header + state.ViewportHeight)
            {
                DrawFileRow(g, state, items[row], row, top, width);
            }
        }

        GdipResetClip(g);
        DrawFilesThumb(g, state, width, header);
    }

    private static void DrawFileRow(nint g, FilesPanelState state, HistoryEntry entry, int row, float top, float width)
    {
        if (row == state.OpenRow)
        {
            var open = RoundedRect(8, top + 2, width - 16, FilesPanelState.OpenHeight - 4, 12);
            Fill(g, _palette == Dark ? Argb(15, 255, 255, 255) : Argb(13, 15, 19, 32), open);
            GdipDeletePath(open);
        }

        // O símbolo acompanha a cor: o erro tem "!", e o status diz o estado.
        if (entry.State == HistoryState.Failed)
        {
            var badge = Ellipse(31, top + 26, 11);
            Fill(g, Argb(255, 210, 59, 59), badge);
            GdipDeletePath(badge);
            Text(g, "!", _semibold, 13, StyleBold, Argb(255, 255, 255, 255), 20, top + 15, 22, 22);
        }
        else
        {
            DrawDocument(g, 20, top + 12, entry.State == HistoryState.Done ? Argb(255, 22, 163, 127) : Tint);
        }

        var problem = entry.State == HistoryState.Failed || entry.Missing;
        var statusInk = problem ? (_palette == Dark ? Argb(255, 255, 138, 138) : Argb(255, 210, 59, 59)) : _palette.SliceInk;
        Text(g, entry.Name, _semibold, 13, StyleRegular, _palette.Ink, 52, top + 7, width - 72, 20, AlignNear);
        Text(g, FilesPanelState.Status(entry), _regular, 11.5f, StyleRegular, statusInk, 52, top + 26, width - 72, 18, AlignNear);
        if (row != state.OpenRow)
        {
            return;
        }

        var first = true;
        foreach (var button in FilesPanelState.Buttons(entry.State))
        {
            var bounds = new PanelRect(button.Bounds.Left, (int)top + button.Bounds.Top, button.Bounds.Right, (int)top + button.Bounds.Bottom);
            var focused = state.Focused == (row, button.Action);
            var filled = first && entry.State == HistoryState.Done;
            var shape = RoundedRect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, CardButtonRadius);
            if (filled)
            {
                // Só Abrir fica azul; em foco, ganha a borda e as franjas da bolha sem perder o azul.
                Fill(g, Argb(255, 47, 106, 255), shape);
                if (focused)
                {
                    Fringe(g, shape);
                }

                GdipDeletePath(shape);
            }
            else if (focused)
            {
                Lens(g, shape, (bounds.Left + bounds.Right) / 2f, (bounds.Top + bounds.Bottom) / 2f, 0, 1);
            }
            else
            {
                GdipDeletePath(shape);
                Surface(g, bounds, CardButtonRadius);
            }

            Text(g, button.Label, _semibold, 13, StyleRegular, filled ? Argb(255, 255, 255, 255) : _palette.Ink, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
            first = false;
        }
    }

    /// <summary>Borda clara e franjas da bolha B, sem o véu, para o botão azul em foco.</summary>
    private static void Fringe(nint g, nint path)
    {
        GdipSetClipPath(g, path, CombineReplace);
        if (_fringes)
        {
            foreach (var (dx, dy, color) in new[] { (2f, 0f, Argb(140, 255, 90, 170)), (-2f, 0f, Argb(153, 60, 200, 255)), (0f, 2f, Argb(140, 255, 230, 120)) })
            {
                GdipTranslateWorldTransform(g, dx, dy, 0);
                Stroke(g, color, 3, path);
                GdipTranslateWorldTransform(g, -dx, -dy, 0);
            }
        }

        GdipResetClip(g);
        Stroke(g, Argb(230, 255, 255, 255), 1.5f, path);
    }

    private static void DrawFilesThumb(nint g, FilesPanelState state, float width, float header)
    {
        if (state.MaxScroll == 0)
        {
            return;
        }

        var track = state.ViewportHeight;
        var thumb = Math.Max(24f, (float)track * state.ViewportHeight / state.ContentHeight);
        var offset = (track - thumb) * state.Scroll / state.MaxScroll;
        var bar = RoundedRect(width - 8, header + offset, 4, thumb, 2);
        Fill(g, WithAlpha(_palette.Ink, 0.25), bar);
        GdipDeletePath(bar);
    }
}
