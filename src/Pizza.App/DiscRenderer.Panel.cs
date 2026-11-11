using Pizza.Core.Conversions;
using Pizza.Core.Visual;
using static Pizza.App.Interop.GdiPlus;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>O que o painel de opções mostra num quadro. <c>Bubbles</c>: posição da bolha em cada grupo.</summary>
internal readonly record struct PanelFrame(
    OptionsPanelState State,
    PanelViewport Viewport,
    string FileName,
    double[] Bubbles,
    double[] Swells,
    PanelPalette Colors);

/// <summary>
/// Painel de opções em vidro (ADR-027, prancha 4b-3). Usa o mesmo vidro, a mesma bolha e a mesma cópia da tela
/// do menu: as duas janelas nunca ficam na tela juntas, e a cópia é zerada ao fechar (ADR-023).
/// </summary>
internal static unsafe partial class DiscRenderer
{
    private const float PanelRadius = 28;
    private const float ButtonRadius = 12;
    private const float KeepBoxSize = 18;
    private const string MetadataNotice = "Este formato não guarda metadados";

    public static void ShowPanel(nint hwnd, int left, int top, PanelFrame frame)
    {
        if (_target == 0)
        {
            return;
        }

        GdipCreateBitmapFromScan0(_width, _height, _width * 4, FormatPargb, _bits, out var bitmap);
        GdipGetImageGraphicsContext(bitmap, out var graphics);
        GdipSetSmoothingMode(graphics, SmoothingAntiAlias);
        GdipSetTextRenderingHint(graphics, TextAntiAlias);
        GdipSetPixelOffsetMode(graphics, PixelOffsetHighQuality);
        GdipGraphicsClear(graphics, 0);
        GdipScaleWorldTransform(graphics, (float)_scale, (float)_scale, 0);

        DrawPanel(graphics, frame);

        GdipDeleteGraphics(graphics);
        GdipDisposeImage(bitmap);

        var screen = GetDC(0);
        var position = new POINT { X = left, Y = top };
        var size = new SIZE { Cx = _width, Cy = _height };
        var source = default(POINT);
        var blend = new BLENDFUNCTION { SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
        UpdateLayeredWindow(hwnd, screen, &position, &size, _memory, &source, 0, &blend, ULW_ALPHA);
        ReleaseDC(0, screen);
    }

    private static void DrawPanel(nint g, PanelFrame frame)
    {
        var state = frame.State;
        var viewport = frame.Viewport;
        var width = (float)state.Width;
        var height = (float)viewport.Height;

        // Vidro do painel: véu mais grosso que o do menu, para o texto pequeno manter o contraste (DES-1, NFR-06).
        var veil = Math.Max(_palette == Dark ? 0.78 : 0.70, _glassAlpha);
        Glass(g, RoundedRect(0.5f, 0.5f, width - 1, height - 1, PanelRadius), veil, 0.7);
        var sheen = RoundedRect(1.5f, 1.5f, width - 3, height - 3, PanelRadius - 1);
        GdipSetClipRect(g, 0, 0, width, 24, CombineReplace);
        Stroke(g, Argb(150, 255, 255, 255), 1.5f, sheen);
        GdipResetClip(g);
        GdipDeletePath(sheen);

        var content = RoundedRect(0, 0, width, viewport.ContentHeight, PanelRadius);
        GdipSetClipPath(g, content, CombineReplace);
        GdipDeletePath(content);
        GdipTranslateWorldTransform(g, 0, -viewport.Offset, 0);
        DrawPanelContent(g, frame, veil);
        GdipTranslateWorldTransform(g, 0, viewport.Offset, 0);
        GdipResetClip(g);

        if (viewport.CanScroll)
        {
            var colors = frame.Colors;
            var previous = viewport.ScrollButton(state.Width, next: false);
            var next = viewport.ScrollButton(state.Width, next: true);
            Text(g, "Role para ver mais", _regular, 13, StyleRegular, Ink(colors.SecondaryText), OptionsPanelState.Padding, previous.Top, previous.Left - 8 - OptionsPanelState.Padding, previous.Bottom - previous.Top, AlignNear);
            PanelButton(g, previous, "↑", accent: false, focused: false, colors);
            PanelButton(g, next, "↓", accent: false, focused: false, colors);
        }
    }

    private static void DrawPanelContent(nint g, PanelFrame frame, double veil)
    {
        var state = frame.State;
        var colors = frame.Colors;
        const float padding = OptionsPanelState.Padding;
        var inner = state.Width - (2 * padding);

        Text(g, $"{frame.FileName} → {state.Converter.Label}", _semibold, 18, StyleBold, Ink(colors.Text), padding, 22, inner, 30, AlignNear);
        Text(g, "Escolha como quer o arquivo", _regular, 13, StyleRegular, Ink(colors.SecondaryText), padding, 54, inner, 22, AlignNear);

        for (var row = 0; row < state.Rows.Count; row++)
        {
            Text(g, state.Rows[row].Label, _semibold, 13, StyleRegular, Ink(colors.Text), padding, OptionsPanelState.RowLabelTop(row), inner, OptionsPanelState.LabelHeight, AlignNear);
        }

        // Opções em vidro claro; depois, uma bolha por grupo, deslizando até a escolhida; o texto por cima.
        foreach (var target in state.Targets.Where(target => target.Kind == PanelTargetKind.Choice))
        {
            Surface(g, target.Bounds, ButtonRadius);
        }

        for (var row = 0; row < state.Rows.Count; row++)
        {
            var (left, right, top, bottom) = state.BubbleAt(row, frame.Bubbles[row]);
            var swell = (float)frame.Swells[row];
            var lens = RoundedRect((float)left - (3 * swell), top - (2 * swell), (float)(right - left) + (6 * swell), bottom - top + (4 * swell), ButtonRadius);
            Lens(g, lens, (float)((left + right) / 2), (top + bottom) / 2f, swell, 1, veil);
        }

        foreach (var target in state.Targets)
        {
            var bounds = target.Bounds;
            switch (target.Kind)
            {
                case PanelTargetKind.Choice:
                    // A cor nunca é o único sinal: a escolhida também ganha marca e negrito.
                    var selected = state.IsSelected(target.Row, target.Choice);
                    var label = state.Rows[target.Row].Choices[target.Choice].Label;
                    Text(g, selected ? "✓ " + label : label, selected ? _semibold : _regular, 13, selected ? StyleBold : StyleRegular, Ink(colors.Text), bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
                    if (selected && state.Focus == (PanelTargetKind.Choice, target.Row))
                    {
                        FocusRing(g, bounds, colors);
                    }

                    break;

                case PanelTargetKind.Keep:
                    DrawKeep(g, bounds.Top, enabled: true, state.KeepMetadata, colors);
                    if (state.Focus.Kind == PanelTargetKind.Keep)
                    {
                        FocusRing(g, bounds, colors);
                    }

                    break;

                case PanelTargetKind.Cancel:
                    PanelButton(g, bounds, "Cancelar  Esc", accent: false, state.Focus.Kind == PanelTargetKind.Cancel, colors);
                    break;

                case PanelTargetKind.Convert:
                    PanelButton(g, bounds, "Converter  Enter", accent: true, state.Focus.Kind == PanelTargetKind.Convert, colors);
                    break;
            }
        }

        if (!state.CanKeepMetadata)
        {
            DrawKeep(g, state.KeepTop, enabled: false, isChecked: false, colors);
            Text(g, MetadataNotice, _regular, 12, StyleRegular, Ink(colors.SecondaryText), padding, state.KeepTop + OptionsPanelState.KeepRowHeight, inner, 20, AlignNear);
        }
    }

    private static void DrawKeep(nint g, int top, bool enabled, bool isChecked, PanelPalette colors)
    {
        const float left = OptionsPanelState.Padding + 3;
        var boxTop = top + ((OptionsPanelState.KeepRowHeight - KeepBoxSize) / 2);
        var box = RoundedRect(left, boxTop, KeepBoxSize, KeepBoxSize, 4);
        if (isChecked)
        {
            Fill(g, Ink(colors.Accent), box);
        }

        Stroke(g, Ink(enabled ? colors.Text : colors.Border), 1.5f, box);
        GdipDeletePath(box);
        if (isChecked)
        {
            Text(g, "✓", _semibold, 13, StyleBold, Ink(colors.OnAccent), left, boxTop, KeepBoxSize, KeepBoxSize);
        }

        Text(g, "Manter metadados", _regular, 13, StyleRegular, Ink(enabled ? colors.Text : colors.SecondaryText), left + KeepBoxSize + 10, top, 300, OptionsPanelState.KeepRowHeight, AlignNear);
    }

    /// <summary>Converter continua na cor de destaque; os demais botões são vidro claro (ADR-027).</summary>
    private static void PanelButton(nint g, PanelRect bounds, string text, bool accent, bool focused, PanelPalette colors)
    {
        if (accent)
        {
            var shape = RoundedRect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, ButtonRadius);
            Fill(g, Ink(colors.Accent), shape);
            GdipDeletePath(shape);
        }
        else
        {
            Surface(g, bounds, ButtonRadius);
        }

        Text(g, text, _semibold, 13, StyleRegular, Ink(accent ? colors.OnAccent : colors.Text), bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        if (focused)
        {
            FocusRing(g, bounds, colors);
        }
    }

    /// <summary>Superfície de vidro clara dos botões e opções, com borda fina (prancha 4b-3).</summary>
    private static void Surface(nint g, PanelRect bounds, float radius)
    {
        var shape = RoundedRect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, radius);
        Fill(g, _palette == Dark ? Argb(179, 36, 40, 52) : Argb(140, 255, 255, 255), shape);
        Stroke(g, _palette == Dark ? Argb(56, 255, 255, 255) : Argb(31, 15, 19, 32), 1, shape);
        GdipDeletePath(shape);
    }

    private static void FocusRing(nint g, PanelRect bounds, PanelPalette colors)
    {
        var ring = RoundedRect(bounds.Left + 1, bounds.Top + 1, bounds.Right - bounds.Left - 2, bounds.Bottom - bounds.Top - 2, ButtonRadius);
        Stroke(g, Ink(colors.Text), 2, ring);
        GdipDeletePath(ring);
    }

    /// <summary>COLORREF da paleta do painel para ARGB opaco do GDI+.</summary>
    private static uint Ink(uint colorRef) =>
        Argb(255, (byte)(colorRef & 0xFF), (byte)((colorRef >> 8) & 0xFF), (byte)((colorRef >> 16) & 0xFF));
}
