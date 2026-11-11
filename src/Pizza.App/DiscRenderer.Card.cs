using Pizza.Core.Conversions;
using Pizza.Core.Notifications;
using static Pizza.App.Interop.GdiPlus;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>O que um aviso mostra. <c>Kind</c> nulo é o cartão "+N".</summary>
internal readonly record struct CardFrame(NotificationKind? Kind, string Title, string Detail, int Overflow, int Percent, string Time, bool Paused);

/// <summary>
/// Avisos do canto da tela (ADR-028, prancha 4c-1): vidro translúcido com transparência por pixel. O fundo real
/// aparece através do véu, ao vivo, sem nenhuma cópia da tela; o véu é denso o bastante para o texto (NFR-06).
/// </summary>
internal static unsafe partial class DiscRenderer
{
    private const float CardButtonRadius = 12;
    private const float CardRadius = 20;

    /// <summary>Desenha o aviso e o entrega à janela em camadas, na posição e com o esmaecer do quadro.</summary>
    public static void ShowCard(nint hwnd, int left, int top, int width, int height, int dpi, CardFrame frame, bool dark, double fade)
    {
        // O menu e o painel usam o mesmo estado de desenho; o aviso o devolve como encontrou.
        var (scale, palette) = (_scale, _palette);
        _scale = dpi / 96.0;
        _palette = dark ? Dark : Light;
        var memory = CreateCompatibleDC(0);
        var dib = CreateDib(memory, width, height, out var bits, out var previous);
        try
        {
            GdipCreateBitmapFromScan0(width, height, width * 4, FormatPargb, bits, out var bitmap);
            GdipGetImageGraphicsContext(bitmap, out var graphics);
            GdipSetSmoothingMode(graphics, SmoothingAntiAlias);
            GdipSetTextRenderingHint(graphics, TextAntiAlias);
            GdipSetPixelOffsetMode(graphics, PixelOffsetHighQuality);
            GdipGraphicsClear(graphics, 0);
            GdipScaleWorldTransform(graphics, (float)_scale, (float)_scale, 0);

            DrawCard(graphics, frame, (float)(width / _scale), (float)(height / _scale));

            GdipDeleteGraphics(graphics);
            GdipDisposeImage(bitmap);

            var screen = GetDC(0);
            var position = new POINT { X = left, Y = top };
            var size = new SIZE { Cx = width, Cy = height };
            var source = default(POINT);
            var blend = new BLENDFUNCTION { SourceConstantAlpha = (byte)Math.Clamp(Math.Round(fade * 255), 0, 255), AlphaFormat = AC_SRC_ALPHA };
            UpdateLayeredWindow(hwnd, screen, &position, &size, memory, &source, 0, &blend, ULW_ALPHA);
            ReleaseDC(0, screen);
        }
        finally
        {
            SelectObject(memory, previous);
            DeleteObject(dib);
            DeleteDC(memory);
            (_scale, _palette) = (scale, palette);
        }
    }

    private static void DrawCard(nint g, CardFrame frame, float width, float height)
    {
        // Véu translúcido do tema, borda clara e brilho no alto, como o vidro do menu.
        var body = RoundedRect(0.5f, 0.5f, width - 1, height - 1, CardRadius);
        Fill(g, _palette == Dark ? Argb(240, 30, 34, 46) : Argb(222, 250, 251, 253), body);
        Stroke(g, _palette == Dark ? Argb(56, 255, 255, 255) : Argb(190, 255, 255, 255), 1, body);
        GdipDeletePath(body);
        var sheen = RoundedRect(1.5f, 1.5f, width - 3, height - 3, CardRadius - 1);
        GdipSetClipRect(g, 0, 0, width, 18, CombineReplace);
        Stroke(g, Argb(150, 255, 255, 255), 1.2f, sheen);
        GdipResetClip(g);
        GdipDeletePath(sheen);
        if (frame.Kind is not { } kind)
        {
            Text(g, $"+{frame.Overflow}", _semibold, 13, StyleRegular, _palette.SliceInk, 0, 0, width, height);
            return;
        }

        // O símbolo acompanha a cor: ela nunca é a única pista (o erro tem "!", e o título diz o estado).
        if (kind == NotificationKind.Error)
        {
            var badge = Ellipse(30, 26, 13);
            Fill(g, Argb(255, 210, 59, 59), badge);
            GdipDeletePath(badge);
            Text(g, "!", _semibold, 14, StyleBold, Argb(255, 255, 255, 255), 17, 13, 26, 26);
        }
        else
        {
            DrawDocument(g, 19, 11, kind == NotificationKind.Success ? Argb(255, 22, 163, 127) : Tint);
        }

        var textWidth = kind == NotificationKind.Progress ? NotificationCardLayout.Buttons(kind)[0].Bounds.Left - 56 : width - 96;
        Text(g, frame.Title, _semibold, 14, StyleBold, _palette.Ink, 50, 12, textWidth, 24, AlignNear);
        Text(g, frame.Detail, _regular, 12, StyleRegular, _palette.SliceInk, 50, 38, kind == NotificationKind.Progress ? textWidth : width - 66, 20, AlignNear);

        if (kind == NotificationKind.Progress)
        {
            DrawCardProgress(g, frame);
        }

        var first = true;
        foreach (var button in NotificationCardLayout.Buttons(kind))
        {
            var bounds = button.Bounds;
            if (button.Action is NotificationAction.Resume or NotificationAction.Pause or NotificationAction.Cancel)
            {
                DrawControl(g, button.Action, bounds, frame.Paused);
                continue;
            }

            if (button.Action == NotificationAction.Close)
            {
                Text(g, button.Label, _regular, 18, StyleRegular, _palette.SliceInk, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
                continue;
            }

            // Prancha 4c-1: só Abrir fica azul; os outros botões são vidro claro.
            var filled = first && kind == NotificationKind.Success;
            if (filled)
            {
                var shape = RoundedRect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, CardButtonRadius);
                Fill(g, Argb(255, 47, 106, 255), shape);
                GdipDeletePath(shape);
            }
            else
            {
                Surface(g, bounds, CardButtonRadius);
            }

            Text(g, button.Label, _semibold, 13, StyleRegular, filled ? Argb(255, 255, 255, 255) : _palette.Ink, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
            first = false;
        }
    }

    /// <summary>
    /// ADR-029, prancha 4c-2: o tempo acima e a linha verde, translúcida mas legível, com a porcentagem no fim.
    /// </summary>
    private static void DrawCardProgress(nint g, CardFrame frame)
    {
        const float left = 16;
        const float width = NotificationCardLayout.Width - 32 - 46;
        const float top = NotificationCardLayout.ProgressBarTop;
        Text(g, frame.Time, _regular, 11.5f, StyleRegular, _palette.SliceInk, left, NotificationCardLayout.ProgressTextTop, NotificationCardLayout.Width - 32, 18, AlignNear);

        var track = RoundedRect(left, top, width, 10, 5);
        Fill(g, WithAlpha(_palette.Ink, 0.12), track);
        GdipDeletePath(track);
        if (frame.Percent > 0)
        {
            var filled = Math.Max(10, width * frame.Percent / 100f);
            var bar = RoundedRect(left, top, filled, 10, 5);
            Gradient(g, bar, top, top + 10, Argb(235, 52, 211, 153), Argb(210, 22, 163, 127));
            GdipSetClipRect(g, left, top, filled, 3, CombineReplace);
            Fill(g, Argb(120, 255, 255, 255), bar);
            GdipResetClip(g);
            GdipDeletePath(bar);
        }

        Text(g, $"{frame.Percent}%", _semibold, 12.5f, StyleBold, _palette.Ink, left + width + 4, top - 5, 42, 20, AlignNear);
    }

    /// <summary>Continuar, pausar e parar: círculo de vidro de 32 px; o ativo ganha a bolha, o que não se aplica fica apagado.</summary>
    private static void DrawControl(nint g, NotificationAction action, PanelRect bounds, bool paused)
    {
        var cx = (bounds.Left + bounds.Right) / 2f;
        var cy = (bounds.Top + bounds.Bottom) / 2f;
        var active = (action == NotificationAction.Pause && !paused) || (action == NotificationAction.Resume && paused);
        var dim = (action == NotificationAction.Resume && !paused) || (action == NotificationAction.Pause && paused);
        var strength = dim ? 0.35 : 1;
        if (active)
        {
            Lens(g, Ellipse(cx, cy, 16), cx, cy, 0, 1);
        }
        else
        {
            var circle = Ellipse(cx, cy, 16);
            Fill(g, WithAlpha(Argb(255, 255, 255, 255), (_palette == Dark ? 0.08 : 0.6) * strength), circle);
            Stroke(g, WithAlpha(_palette == Dark ? Argb(255, 255, 255, 255) : Argb(255, 15, 19, 32), (_palette == Dark ? 0.22 : 0.12) * strength), 1, circle);
            GdipDeletePath(circle);
        }

        var ink = action == NotificationAction.Cancel ? Argb(255, 210, 59, 59) : _palette.Ink;
        ink = WithAlpha(ink, strength);
        GdipCreatePath(0, out var icon);
        switch (action)
        {
            case NotificationAction.Resume:
                GdipAddPathLine(icon, cx - 3, cy - 5, cx + 5, cy);
                GdipAddPathLine(icon, cx + 5, cy, cx - 3, cy + 5);
                GdipClosePathFigure(icon);
                break;
            case NotificationAction.Pause:
                GdipDeletePath(icon);
                icon = RoundedRect(cx - 4.5f, cy - 5, 3, 10, 1);
                Fill(g, ink, icon);
                GdipDeletePath(icon);
                icon = RoundedRect(cx + 1.5f, cy - 5, 3, 10, 1);
                break;
            default:
                GdipDeletePath(icon);
                icon = RoundedRect(cx - 4.5f, cy - 4.5f, 9, 9, 1.5f);
                break;
        }

        Fill(g, ink, icon);
        GdipDeletePath(icon);
    }
    /// <summary>Documento com a faixa colorida do formato, como o ícone do miolo do menu.</summary>
    private static void DrawDocument(nint g, float x, float y, uint band)
    {
        const float k = 22f / 44f;
        GdipCreatePath(0, out var page);
        GdipAddPathLine(page, x + (4 * k), y + (2 * k), x + (30 * k), y + (2 * k));
        GdipAddPathLine(page, x + (30 * k), y + (2 * k), x + (42 * k), y + (14 * k));
        GdipAddPathLine(page, x + (42 * k), y + (14 * k), x + (42 * k), y + (52 * k));
        GdipAddPathLine(page, x + (42 * k), y + (52 * k), x + (2 * k), y + (52 * k));
        GdipAddPathLine(page, x + (2 * k), y + (52 * k), x + (2 * k), y + (2 * k));
        GdipClosePathFigure(page);
        Fill(g, Argb(255, 255, 255, 255), page);
        Stroke(g, Argb(255, 199, 205, 224), 1, page);
        GdipDeletePath(page);

        var stripe = RoundedRect(x + (2 * k), y + (32 * k), 40 * k, 12 * k, 0.1f);
        Fill(g, band, stripe);
        GdipDeletePath(stripe);
    }
}
