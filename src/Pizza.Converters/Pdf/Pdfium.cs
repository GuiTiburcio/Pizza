using System.Runtime.InteropServices;

namespace Pizza.Converters.Pdf;

/// <summary>
/// A API em C do PDFium (ADR-033, adendo de 15/11/2026), só o que o Pizza usa. O PDF chega sempre da memória:
/// nenhum caminho de arquivo chega à biblioteca (SEC-15). A DLL fica ao lado do Worker; o PDFium não é seguro
/// para várias threads, então todo uso passa por <see cref="Gate"/>.
/// </summary>
internal static partial class Pdfium
{
    private const string Library = "pdfium";

    public const ulong ErrorPassword = 4; // FPDF_ERR_PASSWORD
    public const int BitmapBgrx = 3; // FPDFBitmap_BGRx: 4 bytes por pixel, sem transparência

    public static readonly object Gate = new();

    [LibraryImport(Library, EntryPoint = "FPDF_InitLibrary")]
    public static partial void InitLibrary();

    [LibraryImport(Library, EntryPoint = "FPDF_LoadMemDocument")]
    public static unsafe partial nint LoadMemDocument(byte* data, int size, byte* password);

    [LibraryImport(Library, EntryPoint = "FPDF_GetLastError")]
    public static partial ulong GetLastError();

    [LibraryImport(Library, EntryPoint = "FPDF_CloseDocument")]
    public static partial void CloseDocument(nint document);

    [LibraryImport(Library, EntryPoint = "FPDF_GetPageCount")]
    public static partial int GetPageCount(nint document);

    [LibraryImport(Library, EntryPoint = "FPDF_LoadPage")]
    public static partial nint LoadPage(nint document, int index);

    [LibraryImport(Library, EntryPoint = "FPDF_ClosePage")]
    public static partial void ClosePage(nint page);

    [LibraryImport(Library, EntryPoint = "FPDF_GetPageWidth")]
    public static partial double GetPageWidth(nint page);

    [LibraryImport(Library, EntryPoint = "FPDF_GetPageHeight")]
    public static partial double GetPageHeight(nint page);

    [LibraryImport(Library, EntryPoint = "FPDFBitmap_CreateEx")]
    public static partial nint BitmapCreateEx(int width, int height, int format, nint buffer, int stride);

    [LibraryImport(Library, EntryPoint = "FPDFBitmap_FillRect")]
    public static partial void BitmapFillRect(nint bitmap, int left, int top, int width, int height, uint color);

    [LibraryImport(Library, EntryPoint = "FPDFBitmap_Destroy")]
    public static partial void BitmapDestroy(nint bitmap);

    [LibraryImport(Library, EntryPoint = "FPDF_RenderPageBitmap")]
    public static partial void RenderPageBitmap(nint bitmap, nint page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    [LibraryImport(Library, EntryPoint = "FPDFText_LoadPage")]
    public static partial nint TextLoadPage(nint page);

    [LibraryImport(Library, EntryPoint = "FPDFText_ClosePage")]
    public static partial void TextClosePage(nint textPage);

    [LibraryImport(Library, EntryPoint = "FPDFText_CountChars")]
    public static partial int TextCountChars(nint textPage);

    [LibraryImport(Library, EntryPoint = "FPDFText_GetText")]
    public static unsafe partial int TextGetText(nint textPage, int start, int count, ushort* result);
}
