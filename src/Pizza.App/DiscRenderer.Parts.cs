using Pizza.Core.Gesture;
using Pizza.Core.Visual;
using static Pizza.App.Interop.GdiPlus;

namespace Pizza.App;

/// <summary>Partes do menu: fatias, miolo, ícone do arquivo e barra de abas (WF-1, WF-2).</summary>
internal static unsafe partial class DiscRenderer
{
    private static void DrawSlice(nint g, DiscFrame frame, int slot)
    {
        // O lado do texto vem do repouso (rest), para não trocar enquanto as fatias giram na troca de aba.
        var rest = RadialMenu.SlotAngle(slot);
        var angle = rest + frame.Rotation;
        var pulse = frame.PulseSlot == slot;
        var hover = pulse ? 1 : frame.Hover[slot];
        var movement = frame.Motion == MotionStyle.Slide ? hover : 0;
        var shape = SliceGeometry.Build(angle, movement, DiscLayout.CenterX, DiscLayout.CenterY);
        var path = SlicePath(shape);

        // Pelo contorno inteiro: nas fatias de cima e de baixo, o arco passa além dos cantos, e o degradê
        // recomeçaria nessa sobra, desenhando uma linha.
        RectF box;
        GdipGetPathWorldBounds(path, &box, 0, 0);
        var top = box.Y;
        var bottom = box.Y + box.Height;

        if (hover > 0)
        {
            // Brilho colorido em volta (WF-2: sombra colorida, 45%).
            foreach (var (width, alpha) in new[] { (12f, 0.10), (7f, 0.16), (3f, 0.22) })
            {
                Stroke(g, WithAlpha(Tint, alpha * hover), width, path);
            }
        }

        // Formato escolhido: o mesmo vidro do hover, com a cor um pouco mais forte e sem marca; o ✓ fica só no
        // aviso de concluído.
        var fromColor = pulse ? WithAlpha(Tint, 0.74) : Mix(_palette.RestTop, WithAlpha(Tint, 0.62), hover);
        var toColor = pulse ? WithAlpha(Tint, 0.44) : Mix(_palette.RestBottom, WithAlpha(Tint, 0.30), hover);
        Gradient(g, path, top, bottom, fromColor, toColor);
        if (hover > 0)
        {
            // Brilho só no alto e discreto. O degradê do GDI+ se repete fora do trecho dele, por isso o recorte.
            var sheenBottom = top + ((bottom - top) * 0.4f);
            GdipSetClipRect(g, 0, top - 1, DiscLayout.Width, sheenBottom - top + 1, CombineReplace);
            Gradient(g, path, top, sheenBottom, Argb((byte)(110 * hover), 255, 255, 255), Argb(0, 255, 255, 255));
            GdipResetClip(g);
        }

        Stroke(g, Mix(_palette.RestBorder, Argb(242, 255, 255, 255), hover), 1 + (float)hover, path);
        GdipDeletePath(path);
    }

    /// <summary>Bolha de vidro das fatias: passa de uma fatia para a outra, entre o fundo e o texto (ADR-027).</summary>
    private static void DrawSliceLens(nint g, DiscFrame frame)
    {
        var strength = frame.PulseSlot is null ? frame.Hover.DefaultIfEmpty(0).Max() : 1;
        if (strength < 0.01 || frame.Slices.Count == 0)
        {
            return;
        }

        var step = RadialMenu.SlotAngle(1) - RadialMenu.SlotAngle(0);
        var glide = frame.PulseSlot ?? frame.SliceGlide;
        var angle = RadialMenu.SlotAngle(0) + (glide * step) + frame.Rotation;
        var movement = frame.Motion == MotionStyle.Slide ? strength + (0.3 * frame.SliceSwell) : 0;
        var shape = SliceGeometry.Build(angle, movement, DiscLayout.CenterX, DiscLayout.CenterY);
        Lens(g, SlicePath(shape), DiscLayout.CenterX, DiscLayout.CenterY, frame.SliceSwell, 0.6 * strength);
    }

    private static void DrawSliceLabel(nint g, DiscFrame frame, int slot)
    {
        var rest = RadialMenu.SlotAngle(slot);
        var angle = rest + frame.Rotation;
        var pulse = frame.PulseSlot == slot;
        var hover = pulse ? 1 : frame.Hover[slot];
        var movement = frame.Motion == MotionStyle.Slide ? hover : 0;
        var slice = frame.Slices[slot];
        // Nome e descrição seguem o arco da fatia, sempre de pé (escolha "B · Curva completa", Ref-Texto-Fatias).
        var ink = pulse ? Argb(255, 255, 255, 255) : Mix(_palette.SliceInk, _palette.HoverInk, hover);
        var bold = hover >= 0.5;
        CurvedLabel(g, slice.Label, bold ? _regular : _semibold, 14 + (2 * (float)hover), bold ? StyleBold : StyleRegular, ink, angle, rest, CurvedText.NameRadius(angle, movement, rest));
        CurvedLabel(g, slice.Caption, _regular, 10.5f, StyleRegular, ink, angle, rest, CurvedText.CaptionRadius(angle, movement, rest));
    }

    private static void DrawCenter(nint g, DiscFrame frame)
    {
        const float cx = DiscLayout.CenterX;
        const float cy = DiscLayout.CenterY;
        var center = Ellipse(cx, cy, 76);
        Fill(g, _palette.Center, center);
        Stroke(g, _palette.CenterBorder, 1, center);
        GdipDeletePath(center);
        // Brilho rente à borda do miolo (r 76). Mais para dentro, ele encostava nos traços de aba (r 64).
        Arc(g, _palette.Sheen, 1.2f, cx, cy, 73, -151, 61);

        // Um traço por aba, na ordem da barra (WF-1, nota 4).
        for (var tab = 0; tab < MenuTabs.Names.Count; tab++)
        {
            var lit = Math.Max(0, 1 - Math.Abs(Distance(tab, frame.TabPosition)));
            Arc(g, Mix(_palette.IdleArc, Tint, lit), 3 + (float)lit, cx, cy, 64, -90 + (90 * tab) - (TabArcSpan / 2), TabArcSpan, round: true);
        }

        DrawFileIcon(g, cx - 18, cy - 40, frame.FileName);
        if (frame.FileCount > 1)
        {
            Text(g, $"{frame.FileCount} arquivos", _semibold, 12, StyleRegular, _palette.Ink, cx - 60, cy + 10, 120, 18);
            Text(g, "um arquivo por vez", _regular, 10.5f, StyleRegular, _palette.Ink, cx - 60, cy + 28, 120, 16);
        }
        else if (frame.FileName is { } fileName)
        {
            Text(g, fileName, _semibold, 10.5f, StyleRegular, _palette.Ink, cx - 60, cy + 12, 120, 18);
        }

        if (frame.TurnCount > 1)
        {
            for (var turn = 0; turn < frame.TurnCount; turn++)
            {
                var dot = Ellipse(cx + ((turn - ((frame.TurnCount - 1) / 2f)) * 12), cy + 43, 3);
                Fill(g, turn == frame.Turn ? Tint : _palette.IdleArc, dot);
                GdipDeletePath(dot);
            }
        }

        if (frame.Slices.Count == 0)
        {
            Text(g, "Em breve", _semibold, 16, StyleRegular, _palette.Ink, cx - 90, cy - 132, 180, 30);
        }
    }

    /// <summary>Documento com a faixa colorida do tipo, como no design aprovado.</summary>
    private static void DrawFileIcon(nint g, float x, float y, string? fileName)
    {
        const float k = 36f / 44f;
        var band = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant() switch
        {
            ".pdf" => Argb(255, 210, 59, 59),
            ".doc" or ".docx" or ".odt" or ".rtf" => Argb(255, 43, 108, 240),
            _ => Argb(255, 227, 138, 30),
        };

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

    private static void DrawTabBar(nint g, DiscFrame frame)
    {
        var bar = RoundedRect(DiscLayout.TabBarLeft, 0, DiscLayout.TabBarWidth, DiscLayout.TabBarHeight, DiscLayout.TabBarHeight / 2f);
        Glass(g, bar, Math.Max(0.30, _glassAlpha), _palette.Border + 0.05);

        // A bolha de vidro marca a aba e desliza com o carrossel, crescendo no meio do caminho (ADR-027).
        var swell = (float)Math.Sin(Math.PI * (frame.TabPosition - Math.Floor(frame.TabPosition)));
        var lensLeft = (float)DiscLayout.HighlightLeft(frame.TabPosition) - (3 * swell);
        var height = DiscLayout.TabBarHeight - (2 * DiscLayout.TabPadding);
        var lens = RoundedRect(lensLeft, DiscLayout.TabPadding - (2 * swell), DiscLayout.TabWidth + (6 * swell), height + (4 * swell), (height / 2f) + (2 * swell));
        Lens(g, lens, lensLeft + (DiscLayout.TabWidth / 2f), DiscLayout.TabBarHeight / 2f, swell, 1, Math.Max(0.30, _glassAlpha));

        // Barra B (ADR-026): ícone em cima e o nome curto embaixo.
        for (var tab = 0; tab < MenuTabs.Names.Count; tab++)
        {
            var (left, _) = DiscLayout.TabSpan(tab);
            var active = tab == frame.TargetTab;
            var ink = active ? _palette.Ink : _palette.SliceInk;
            DrawTabIcon(g, tab, (float)left + ((DiscLayout.TabWidth - DiscLayout.TabIconSize) / 2f), DiscLayout.TabIconTop, ink);
            Text(g, MenuTabs.ShortNames[tab], active ? _semibold : _regular, 11.5f, StyleRegular, ink, (float)left, DiscLayout.TabLabelTop, DiscLayout.TabWidth, DiscLayout.TabLabelHeight);
        }
    }

    /// <summary>Faixa de níveis da fatia em hover (ADR-027, prancha 4b-1), com a bolha de vidro no nível escolhido.</summary>
    private static void DrawStrip(nint g, DiscFrame frame)
    {
        if (frame.StripTitle is not { } title || frame.Levels is not { } levels)
        {
            return;
        }

        const float titleLeft = (DiscLayout.Width - DiscLayout.StripTitleWidth) / 2f;
        var chip = RoundedRect(titleLeft, DiscLayout.StripTop, DiscLayout.StripTitleWidth, DiscLayout.StripTitleHeight, DiscLayout.StripTitleHeight / 2f);
        Glass(g, chip, Math.Max(0.40, _glassAlpha), _palette.Border + 0.05);
        Text(g, title, _semibold, 11.5f, StyleRegular, _palette.Ink, titleLeft, DiscLayout.StripTop, DiscLayout.StripTitleWidth, DiscLayout.StripTitleHeight);

        for (var level = 0; level < levels.Length; level++)
        {
            var left = (float)DiscLayout.LevelSpan(level, levels.Length).Left;
            if (level == frame.LevelHover)
            {
                // O mesmo brilho colorido da fatia em hover.
                var glow = RoundedRect(left, DiscLayout.LevelTop, DiscLayout.LevelWidth, DiscLayout.LevelHeight, 13);
                foreach (var (width, alpha) in new[] { (9f, 0.12), (5f, 0.18), (2f, 0.26) })
                {
                    Stroke(g, WithAlpha(Tint, alpha), width, glow);
                }

                GdipDeletePath(glow);
            }

            Glass(g, RoundedRect(left, DiscLayout.LevelTop, DiscLayout.LevelWidth, DiscLayout.LevelHeight, 13), Math.Max(0.30, _glassAlpha), _palette.Border);
        }

        // A bolha marca o nível escolhido e desliza até o próximo quando a roda troca o nível (ADR-027).
        var swell = (float)frame.LevelSwell;
        var lensLeft = (float)(DiscLayout.LevelSpan(0, levels.Length).Left + (frame.LevelGlide * (DiscLayout.LevelWidth + DiscLayout.LevelGap))) - (3 * swell);
        var lens = RoundedRect(lensLeft, DiscLayout.LevelTop - (2 * swell), DiscLayout.LevelWidth + (6 * swell), DiscLayout.LevelHeight + (4 * swell), 13);
        Lens(g, lens, lensLeft + (DiscLayout.LevelWidth / 2f), DiscLayout.LevelTop + (DiscLayout.LevelHeight / 2f), swell, 1, Math.Max(0.30, _glassAlpha));

        for (var level = 0; level < levels.Length; level++)
        {
            var left = (float)DiscLayout.LevelSpan(level, levels.Length).Left;
            var picked = level == frame.Level;
            var (label, detail) = levels[level];
            var labelTop = detail is null ? DiscLayout.LevelTop : DiscLayout.LevelTop + 4;
            var labelHeight = detail is null ? DiscLayout.LevelHeight : 20;
            Text(g, label, picked ? _semibold : _regular, 12, StyleRegular, _palette.Ink, left, labelTop, DiscLayout.LevelWidth, labelHeight);
            if (detail is not null)
            {
                Text(g, detail, _regular, 10, StyleRegular, _palette.SliceInk, left, DiscLayout.LevelTop + 22, DiscLayout.LevelWidth, 16);
            }
        }
    }

    private static double Distance(int tab, double position)
    {
        var count = MenuTabs.Names.Count;
        var distance = (tab - position) % count;
        return distance > count / 2.0 ? distance - count : distance < -count / 2.0 ? distance + count : distance;
    }
}
