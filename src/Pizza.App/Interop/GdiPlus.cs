using System.Runtime.InteropServices;

namespace Pizza.App.Interop;

/// <summary>API plana do GDI+, que já vem no Windows (ADR-023). Sem pacote: só chamadas a gdiplus.dll.</summary>
internal static unsafe partial class GdiPlus
{
    public const int FormatArgb = 0x0026200A;
    public const int FormatPargb = 0x000E200B;
    public const int SmoothingAntiAlias = 4;
    public const int TextAntiAlias = 4;
    public const int PixelOffsetHighQuality = 4;
    public const int UnitPixel = 2;
    public const int UnitWorld = 0;
    public const int StyleRegular = 0;
    public const int StyleBold = 1;
    public const int AlignCenter = 1;
    public const int TrimmingEllipsis = 3;
    public const int NoWrap = 0x1000;
    public const int LineJoinRound = 2;
    public const int LineCapRound = 2;
    public const int CombineReplace = 0;

    private const string Library = "gdiplus.dll";

    [StructLayout(LayoutKind.Sequential)]
    public struct StartupInput
    {
        public uint Version;
        public nint Callback;
        public int SuppressBackgroundThread;
        public int SuppressExternalCodecs;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PointF
    {
        public float X;
        public float Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RectF
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
    }

    [LibraryImport(Library)]
    public static partial int GdiplusStartup(out nuint token, StartupInput* input, nint output);

    [LibraryImport(Library)]
    public static partial int GdipCreateBitmapFromScan0(int width, int height, int stride, int format, nint scan0, out nint bitmap);

    [LibraryImport(Library)]
    public static partial int GdipGetImageGraphicsContext(nint image, out nint graphics);

    [LibraryImport(Library)]
    public static partial int GdipDeleteGraphics(nint graphics);

    [LibraryImport(Library)]
    public static partial int GdipDisposeImage(nint image);

    [LibraryImport(Library)]
    public static partial int GdipSetSmoothingMode(nint graphics, int mode);

    [LibraryImport(Library)]
    public static partial int GdipSetTextRenderingHint(nint graphics, int hint);

    [LibraryImport(Library)]
    public static partial int GdipSetPixelOffsetMode(nint graphics, int mode);

    [LibraryImport(Library)]
    public static partial int GdipGraphicsClear(nint graphics, uint color);

    [LibraryImport(Library)]
    public static partial int GdipScaleWorldTransform(nint graphics, float sx, float sy, int order);

    [LibraryImport(Library)]
    public static partial int GdipResetWorldTransform(nint graphics);

    [LibraryImport(Library)]
    public static partial int GdipCreatePath(int fillMode, out nint path);

    [LibraryImport(Library)]
    public static partial int GdipDeletePath(nint path);

    [LibraryImport(Library)]
    public static partial int GdipAddPathArc(nint path, float x, float y, float width, float height, float start, float sweep);

    [LibraryImport(Library)]
    public static partial int GdipAddPathBezier(nint path, float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4);

    [LibraryImport(Library)]
    public static partial int GdipAddPathLine(nint path, float x1, float y1, float x2, float y2);

    [LibraryImport(Library)]
    public static partial int GdipAddPathEllipse(nint path, float x, float y, float width, float height);

    [LibraryImport(Library)]
    public static partial int GdipClosePathFigure(nint path);

    [LibraryImport(Library)]
    public static partial int GdipCreateSolidFill(uint color, out nint brush);

    [LibraryImport(Library)]
    public static partial int GdipCreateLineBrush(PointF* from, PointF* to, uint color1, uint color2, int wrap, out nint brush);

    [LibraryImport(Library)]
    public static partial int GdipDeleteBrush(nint brush);

    [LibraryImport(Library)]
    public static partial int GdipFillPath(nint graphics, nint brush, nint path);

    [LibraryImport(Library)]
    public static partial int GdipFillEllipse(nint graphics, nint brush, float x, float y, float width, float height);

    [LibraryImport(Library)]
    public static partial int GdipCreatePen1(uint color, float width, int unit, out nint pen);

    [LibraryImport(Library)]
    public static partial int GdipSetPenLineJoin(nint pen, int join);

    [LibraryImport(Library)]
    public static partial int GdipSetPenStartCap(nint pen, int cap);

    [LibraryImport(Library)]
    public static partial int GdipSetPenEndCap(nint pen, int cap);

    [LibraryImport(Library)]
    public static partial int GdipDeletePen(nint pen);

    [LibraryImport(Library)]
    public static partial int GdipDrawPath(nint graphics, nint pen, nint path);

    [LibraryImport(Library)]
    public static partial int GdipDrawArc(nint graphics, nint pen, float x, float y, float width, float height, float start, float sweep);

    [LibraryImport(Library)]
    public static partial int GdipDrawLine(nint graphics, nint pen, float x1, float y1, float x2, float y2);

    [LibraryImport(Library)]
    public static partial int GdipSetClipPath(nint graphics, nint path, int combine);

    [LibraryImport(Library)]
    public static partial int GdipResetClip(nint graphics);

    [LibraryImport(Library)]
    public static partial int GdipSetClipRect(nint graphics, float x, float y, float width, float height, int combine);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int GdipCreateFontFamilyFromName(string name, nint collection, out nint family);

    [LibraryImport(Library)]
    public static partial int GdipCreateFont(nint family, float size, int style, int unit, out nint font);

    [LibraryImport(Library)]
    public static partial int GdipDeleteFont(nint font);

    [LibraryImport(Library)]
    public static partial int GdipCreateStringFormat(int flags, ushort language, out nint format);

    [LibraryImport(Library)]
    public static partial int GdipSetStringFormatAlign(nint format, int align);

    [LibraryImport(Library)]
    public static partial int GdipSetStringFormatLineAlign(nint format, int align);

    [LibraryImport(Library)]
    public static partial int GdipSetStringFormatTrimming(nint format, int trimming);

    [LibraryImport(Library)]
    public static partial int GdipDeleteStringFormat(nint format);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int GdipDrawString(nint graphics, string text, int length, nint font, RectF* layout, nint format, nint brush);

    [LibraryImport(Library)]
    public static partial int GdipDrawImageRectI(nint graphics, nint image, int x, int y, int width, int height);
}
