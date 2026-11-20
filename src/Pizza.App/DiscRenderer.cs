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
    bool Choosing = false,
    string? StripTitle = null,
    (string Label, string? Detail)[]? Levels = null,
    int Level = 0,
    int? LevelHover = null,
    double SliceGlide = 0,
    double SliceSwell = 0,
    double LevelGlide = 0,
    double LevelSwell = 0,
    bool[]? Unavailable = null,
    IReadOnlyList<(string Number, string Name)>? Order = null,
    bool Merging = false,
    int? HintSlot = null,
    string? Hint = null);

/// <summary>
/// Desenha o menu no visual do Dossiê (DES-1, WF-1, WF-2) com GDI+ e transparência por pixel (ADR-023).
/// O vidro desfoca uma cópia do trecho da tela atrás do menu, que só existe enquanto ele está aberto.
/// </summary>
internal static unsafe partial class DiscRenderer
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
        MinimumVeil: 0.35);

    private static Palette _palette = Light;

    // ADR-027: as franjas custam mais por quadro (cerca de 2 ms a 150%). Três quadros seguidos abaixo de 30 por
    // segundo e a bolha fica sem franjas até o menu abrir de novo; a forma não muda.
    private const double FrameBudgetMs = 33;
    private const int SlowFramesToDrop = 3;
    private static bool _fringes = true;
    private static int _slowFrames;

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
    private static double _scale = 1;

    public static void Start()
    {
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
        _scale = dpi / 96.0;
        _height = height;
        _memory = CreateCompatibleDC(0);
        _target = CreateDib(_memory, width, height, out _bits, out _originalTarget);
        _palette = DarkTheme() ? Dark : Light;
        _fringes = true;
        _slowFrames = 0;
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

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
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

        if (_fringes)
        {
            var slow = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds > FrameBudgetMs;
            _slowFrames = slow ? _slowFrames + 1 : 0;
            _fringes = _slowFrames < SlowFramesToDrop;
        }

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

        DrawSliceLens(g, frame);
        for (var slot = 0; slot < frame.Slices.Count; slot++)
        {
            DrawSliceLabel(g, frame, slot);
        }

        DrawCenter(g, frame);
        DrawTabBar(g, frame);
        DrawStrip(g, frame);

        var hint = RoundedRect(DiscLayout.HintLeft, DiscLayout.HintTop, DiscLayout.HintWidth, DiscLayout.HintHeight, DiscLayout.HintHeight / 2f);
        Glass(g, hint, Math.Max(0.40, _glassAlpha), _palette.Border + 0.05);
        Text(g, frame.Choosing ? "Clique no nível ou na fatia · Direito: opções · Esc cancela" : "Solte no nível ou na fatia · Role sobre a faixa para trocar o nível", _regular, 12, StyleRegular, _palette.Ink, DiscLayout.HintLeft + 10, DiscLayout.HintTop, DiscLayout.HintWidth - 20, DiscLayout.HintHeight);
    }

    private static double ScaleOf() => _scale;

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
    internal static bool DarkTheme()
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
