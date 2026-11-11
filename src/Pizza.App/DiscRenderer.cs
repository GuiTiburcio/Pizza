using Pizza.Core.Conversions;
using Pizza.Core.Gesture;
using Pizza.Core.Visual;
using static Pizza.App.Interop.GdiPlus;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>O que o menu mostra num quadro.</summary>
internal readonly record struct DiscFrame(
    double Scale,
    string? FileName,
    int FileCount,
    IReadOnlyList<ConverterInfo> Slices,
    double[] Hover,
    double TabPosition,
    int TargetTab,
    double Rotation,
    MotionStyle Motion,
    int? PulseSlot,
    int Turn = 0,
    int TurnCount = 1,
    bool Choosing = false);

internal readonly record struct SliceLabels(RectF Title, RectF Caption, RectF CaptionInk, int CaptionLines);

/// <summary>
/// Desenha o menu no visual do Dossiê (DES-1, WF-1, WF-2) com GDI+ e transparência por pixel (ADR-023).
/// O vidro desfoca uma cópia do trecho da tela atrás do menu, que só existe enquanto ele está aberto.
/// </summary>
internal static unsafe class DiscRenderer
{
    private const uint SourceCopy = 0x00CC0020;
    private const int BlurRadius = 11;
    private const double Saturation = 1.8;
    private const double TabArcSpan = 30;

    private static readonly uint Tint = Argb(255, 47, 107, 255);

    // Parte 4.2 (claro) e 4.4 (escuro): mesmo desenho, com o vidro escurecido e textos claros.
    private static readonly Palette Light = new(
        Veil: Argb(255, 255, 255, 255),
        Border: 0.55,
        Ink: Argb(255, 15, 19, 32),
        SliceInk: Argb(255, 31, 38, 56),
        HoverInk: Argb(255, 11, 21, 48),
        RestTop: Argb(51, 26, 33, 64),
        RestBottom: Argb(20, 26, 33, 64),
        RestBorder: Argb(115, 255, 255, 255),
        Center: Argb(140, 255, 255, 255),
        CenterBorder: Argb(235, 255, 255, 255),
        Sheen: Argb(204, 255, 255, 255),
        IdleArc: Argb(71, 15, 19, 32),
        Highlight: Argb(217, 255, 255, 255),
        MinimumVeil: 0.16);

    private static readonly Palette Dark = new(
        Veil: Argb(255, 10, 12, 20),
        Border: 0.22,
        Ink: Argb(255, 238, 241, 248),
        SliceInk: Argb(255, 214, 221, 236),
        HoverInk: Argb(255, 255, 255, 255),
        RestTop: Argb(31, 255, 255, 255),
        RestBottom: Argb(13, 255, 255, 255),
        RestBorder: Argb(56, 255, 255, 255),
        Center: Argb(36, 255, 255, 255),
        CenterBorder: Argb(90, 255, 255, 255),
        Sheen: Argb(90, 255, 255, 255),
        IdleArc: Argb(90, 255, 255, 255),
        Highlight: Argb(56, 255, 255, 255),
        MinimumVeil: 0.35);

    private static Palette _palette = Light;

    private static nuint _token;
    private static nint _regular;
    private static nint _semibold;
    private static nint _memory;
    private static nint _target;
    private static nint _originalTarget;
    private static nint _bits;
    private static nint _backdropMemory;
    private static nint _backdropDib;
    private static nint _originalBackdrop;
    private static nint _backdropBits;
    private static nint _backdrop;
    private static int _width;
    private static int _height;
    private static double _glassAlpha;

    public static void Start()
    {
        if (_token != 0)
        {
            return;
        }

        var input = new StartupInput { Version = 1 };
        GdiplusStartup(out _token, &input, 0);
        _regular = Family("Segoe UI");
        _semibold = Family("Segoe UI Semibold");
    }

    /// <summary>Prepara a janela de desenho e, com a transparência ligada, o vidro desfocado.</summary>
    public static void Prepare(int left, int top, int width, int height, int dpi)
    {
        Release();
        _width = width;
        _height = height;
        _memory = CreateCompatibleDC(0);
        _target = CreateDib(_memory, width, height, out _bits, out _originalTarget);
        _palette = DarkTheme() ? Dark : Light;
        _glassAlpha = _palette.MinimumVeil;
        if (!TransparencyEnabled())
        {
            return;
        }

        _backdropMemory = CreateCompatibleDC(0);
        _backdropDib = CreateDib(_backdropMemory, width, height, out _backdropBits, out _originalBackdrop);
        var screen = GetDC(0);

        // Sem CAPTUREBLT: janelas em camadas, como a imagem do arraste, ficam fora da cópia.
        BitBlt(_backdropMemory, 0, 0, width, height, screen, left, top, SourceCopy);
        ReleaseDC(0, screen);

        var pixels = new Span<byte>((void*)_backdropBits, width * height * 4);
        GlassBlur.Apply(pixels, width, height, BlurRadius * dpi / 96, Saturation);

        // Além do Dossiê: o véu engrossa quando o fundo puxa para o lado do texto, que continua legível.
        var luminance = AverageLuminance(pixels);
        _glassAlpha = _palette == Dark
            ? Math.Max(Dark.MinimumVeil, 1 - (0.30 / Math.Max(luminance, 0.30)))
            : Math.Max(Light.MinimumVeil, (0.72 - luminance) / (1 - luminance));
        GdipCreateBitmapFromScan0(width, height, width * 4, FormatArgb, _backdropBits, out _backdrop);
    }

    /// <summary>Descarta tudo, zerando a cópia da tela antes de soltar a memória.</summary>
    public static void Release()
    {
        if (_backdrop != 0)
        {
            GdipDisposeImage(_backdrop);
            _backdrop = 0;
        }

        if (_backdropBits != 0)
        {
            new Span<byte>((void*)_backdropBits, _width * _height * 4).Clear();
            SelectObject(_backdropMemory, _originalBackdrop);
            DeleteObject(_backdropDib);
            DeleteDC(_backdropMemory);
            _backdropBits = 0;
        }

        if (_target != 0)
        {
            // O quadro composto também contém pixels da tela: zerar antes de liberar (ADR-023).
            new Span<byte>((void*)_bits, _width * _height * 4).Clear();
            SelectObject(_memory, _originalTarget);
            DeleteObject(_target);
            DeleteDC(_memory);
            _target = 0;
        }
    }

    public static void Show(nint hwnd, int left, int top, DiscFrame frame)
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
        GdipScaleWorldTransform(graphics, (float)frame.Scale, (float)frame.Scale, 0);

        Draw(graphics, frame);

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

    private static void Draw(nint g, DiscFrame frame)
    {
        const float cx = DiscLayout.CenterX;
        const float cy = DiscLayout.CenterY;

        // Sombra externa suave, um pouco abaixo do disco.
        for (var ring = 8; ring >= 1; ring--)
        {
            var shadow = Ellipse(cx, cy + 6, 190 + (ring * 2));
            Fill(g, Argb(4, 15, 20, 40), shadow);
            GdipDeletePath(shadow);
        }

        Glass(g, Ellipse(cx, cy, 190), _glassAlpha, _palette.Border);
        Arc(g, _palette.Sheen, 1.5f, cx, cy, 186, -160, 81);

        for (var slot = 0; slot < frame.Slices.Count; slot++)
        {
            DrawSlice(g, frame, slot);
        }

        DrawCenter(g, frame);
        DrawTabBar(g, frame);

        var hint = RoundedRect(DiscLayout.HintLeft, DiscLayout.HintTop, DiscLayout.HintWidth, DiscLayout.HintHeight, DiscLayout.HintHeight / 2f);
        Glass(g, hint, Math.Max(0.40, _glassAlpha), _palette.Border + 0.05);
        Text(g, frame.Choosing ? "Clique no formato · Role para mais · Esc cancela" : "Solte onde estiver e clique · Ou solte no formato", _regular, 12, StyleRegular, _palette.Ink, DiscLayout.HintLeft + 10, DiscLayout.HintTop, DiscLayout.HintWidth - 20, DiscLayout.HintHeight);
    }

    private static void DrawSlice(nint g, DiscFrame frame, int slot)
    {
        var angle = RadialMenu.SlotAngle(slot) + frame.Rotation;
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

        if (pulse)
        {
            Fill(g, WithAlpha(Tint, 0.88), path);
        }
        else
        {
            Gradient(g, path, top, bottom, Mix(_palette.RestTop, WithAlpha(Tint, 0.62), hover), Mix(_palette.RestBottom, WithAlpha(Tint, 0.30), hover));
            if (hover > 0)
            {
                // Brilho só no alto e discreto. O degradê do GDI+ se repete fora do trecho dele, por isso o recorte.
                var sheenBottom = top + ((bottom - top) * 0.4f);
                GdipSetClipRect(g, 0, top - 1, DiscLayout.Width, sheenBottom - top + 1, CombineReplace);
                Gradient(g, path, top, sheenBottom, Argb((byte)(110 * hover), 255, 255, 255), Argb(0, 255, 255, 255));
                GdipResetClip(g);
            }
        }

        Stroke(g, Mix(_palette.RestBorder, Argb(242, 255, 255, 255), hover), 1 + (float)hover, path);
        GdipDeletePath(path);

        var slice = frame.Slices[slot];
        var labels = LayoutLabels(g, slice, angle, movement, hover, pulse);
        var ink = pulse ? Argb(255, 255, 255, 255) : Mix(_palette.SliceInk, _palette.HoverInk, hover);
        var name = pulse ? "✓ " + slice.Label : slice.Label;
        var bold = hover >= 0.5;
        Text(g, name, bold ? _regular : _semibold, 14 + (2 * (float)hover), bold ? StyleBold : StyleRegular, ink, labels.Title.X, labels.Title.Y, labels.Title.Width, labels.Title.Height, ellipsis: false);
        Text(g, slice.Caption, _regular, 10.5f, StyleRegular, ink, labels.Caption.X, labels.Caption.Y, labels.Caption.Width, labels.Caption.Height, wrap: true, ellipsis: false);
    }

    /// <summary>Leitura horizontal; a descrição usa até duas linhas em vez de atravessar o contorno.</summary>
    internal static SliceLabels LayoutLabels(nint g, ConverterInfo slice, double angle, double movement, double hover, bool pulse)
    {
        const float captionWidth = 64;
        var bold = hover >= 0.5;
        var name = pulse ? "✓ " + slice.Label : slice.Label;
        var title = Measure(g, name, bold ? _regular : _semibold, 14 + (2 * (float)hover), bold ? StyleBold : StyleRegular, 110, wrap: false, out _);
        var caption = Measure(g, slice.Caption, _regular, 10.5f, StyleRegular, captionWidth, wrap: true, out var lines);
        var label = SliceGeometry.LabelCenter(angle, movement, DiscLayout.CenterX, DiscLayout.CenterY);
        var top = (float)label.Y - ((title.Height + caption.Height + 2) / 2);
        title.X = (float)label.X - (title.Width / 2);
        title.Y = top;
        var captionInk = new RectF { X = (float)label.X - (caption.Width / 2), Y = top + title.Height + 2, Width = caption.Width, Height = caption.Height };
        var captionBox = new RectF { X = (float)label.X - (captionWidth / 2), Y = captionInk.Y, Width = captionWidth, Height = caption.Height };
        return new SliceLabels(title, captionBox, captionInk, lines);
    }

    private static void DrawCenter(nint g, DiscFrame frame)
    {
        const float cx = DiscLayout.CenterX;
        const float cy = DiscLayout.CenterY;
        var center = Ellipse(cx, cy, 76);
        Fill(g, _palette.Center, center);
        Stroke(g, _palette.CenterBorder, 1, center);
        GdipDeletePath(center);

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

        var highlight = RoundedRect((float)DiscLayout.HighlightLeft(frame.TabPosition), DiscLayout.TabPadding, DiscLayout.TabWidth, DiscLayout.TabBarHeight - (2 * DiscLayout.TabPadding), 22);
        Fill(g, _palette.Highlight, highlight);
        GdipDeletePath(highlight);

        for (var tab = 0; tab < MenuTabs.Names.Count; tab++)
        {
            var (left, _) = DiscLayout.TabSpan(tab);
            var active = tab == frame.TargetTab;
            Text(g, MenuTabs.Names[tab], active ? _semibold : _regular, 13, StyleRegular, active ? _palette.Ink : _palette.SliceInk, (float)left, DiscLayout.TabPadding, DiscLayout.TabWidth, DiscLayout.TabBarHeight - (2 * DiscLayout.TabPadding));
        }
    }

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

    private static double ScaleOf() => _width / (double)DiscLayout.Width;

    internal static nint SlicePath(SliceShape shape)
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

    private static void Text(nint g, string text, nint family, float size, int style, uint color, float x, float y, float width, float height, bool wrap = false, bool ellipsis = true)
    {
        GdipCreateFont(family, size, style, UnitPixel, out var font);
        GdipCreateStringFormat(wrap ? 0 : NoWrap, 0, out var format);
        GdipSetStringFormatAlign(format, AlignCenter);
        GdipSetStringFormatLineAlign(format, AlignCenter);
        GdipSetStringFormatTrimming(format, ellipsis ? TrimmingEllipsis : 0);
        GdipCreateSolidFill(color, out var brush);
        var layout = new RectF { X = x, Y = y, Width = width, Height = height };
        GdipDrawString(g, text, -1, font, &layout, format, brush);
        GdipDeleteBrush(brush);
        GdipDeleteStringFormat(format);
        GdipDeleteFont(font);
    }

    private static RectF Measure(nint g, string text, nint family, float size, int style, float width, bool wrap, out int lines)
    {
        GdipCreateFont(family, size, style, UnitPixel, out var font);
        GdipCreateStringFormat(wrap ? 0 : NoWrap, 0, out var format);
        var layout = new RectF { Width = width, Height = 1000 };
        RectF bounds;
        int count;
        GdipMeasureString(g, text, -1, font, &layout, format, &bounds, null, &count);
        GdipDeleteStringFormat(format);
        GdipDeleteFont(font);
        lines = count;
        return bounds;
    }

    private static nint Family(string name) =>
        GdipCreateFontFamilyFromName(name, 0, out var family) == 0 ? family : Family("Segoe UI");

    private static double Distance(int tab, double position)
    {
        var count = MenuTabs.Names.Count;
        var distance = (tab - position) % count;
        return distance > count / 2.0 ? distance - count : distance < -count / 2.0 ? distance + count : distance;
    }

    private static double AverageLuminance(Span<byte> bgra)
    {
        double sum = 0;
        var samples = 0;
        for (var i = 0; i < bgra.Length; i += 4 * 16)
        {
            sum += ((bgra[i] * 0.114) + (bgra[i + 1] * 0.587) + (bgra[i + 2] * 0.299)) / 255;
            samples++;
        }

        return samples == 0 ? 1 : sum / samples;
    }

    /// <summary>Segue o tema dos aplicativos no Windows, como a opção padrão "Seguir o Windows" da Parte 4.</summary>
    private static bool DarkTheme()
    {
        uint value = 1;
        var size = (uint)sizeof(uint);
        var result = RegGetValueW(HKEY_CURRENT_USER, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", RRF_RT_REG_DWORD, null, &value, &size);
        return result == 0 && value == 0;
    }

    private static bool TransparencyEnabled()
    {
        uint value = 1;
        var size = (uint)sizeof(uint);
        var result = RegGetValueW(HKEY_CURRENT_USER, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", RRF_RT_REG_DWORD, null, &value, &size);
        return result != 0 || value != 0;
    }

    private static nint CreateDib(nint memory, int width, int height, out nint bits, out nint previous)
    {
        var info = new BITMAPINFOHEADER { Size = (uint)sizeof(BITMAPINFOHEADER), Width = width, Height = -height, Planes = 1, BitCount = 32 };
        nint pixels;
        var dib = CreateDIBSection(memory, &info, 0, &pixels, 0, 0);
        previous = SelectObject(memory, dib);
        bits = pixels;
        return dib;
    }

    private sealed record Palette(
        uint Veil,
        double Border,
        uint Ink,
        uint SliceInk,
        uint HoverInk,
        uint RestTop,
        uint RestBottom,
        uint RestBorder,
        uint Center,
        uint CenterBorder,
        uint Sheen,
        uint IdleArc,
        uint Highlight,
        double MinimumVeil);

    private static uint Argb(byte a, byte r, byte g, byte b) => ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;

    private static uint WithAlpha(uint color, double alpha) => (color & 0x00FFFFFF) | ((uint)Math.Clamp(Math.Round(alpha * 255), 0, 255) << 24);

    private static uint Mix(uint from, uint to, double amount)
    {
        static uint Channel(uint a, uint b, int shift, double t) =>
            (uint)Math.Round(((a >> shift) & 0xFF) + ((((double)((b >> shift) & 0xFF)) - ((a >> shift) & 0xFF)) * t)) << shift;

        return Channel(from, to, 24, amount) | Channel(from, to, 16, amount) | Channel(from, to, 8, amount) | Channel(from, to, 0, amount);
    }
}
