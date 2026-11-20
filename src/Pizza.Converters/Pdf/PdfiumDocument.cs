using System.Runtime.InteropServices;

namespace Pizza.Converters.Pdf;

/// <summary>
/// Um PDF aberto pelo PDFium, todo em memória (ADR-033). O arquivo não é confiável: só roda no Worker em
/// AppContainer, sem rede (SEC-20). O buffer nativo fica fixo até o fim do uso, como na QPDF: o PDFium lê dele
/// o tempo todo.
/// </summary>
internal sealed unsafe class PdfiumDocument : IDisposable
{
    private static bool _initialized;

    private nint _document;
    private byte* _source;

    private PdfiumDocument(nint document, byte* source, int pageCount)
    {
        _document = document;
        _source = source;
        PageCount = pageCount;
    }

    public int PageCount { get; }

    /// <summary>Abre o PDF ou devolve o código de erro da SPECS §10.4. Mesmos limites da QPDF (SEC-42, SEC-44).</summary>
    public static string? TryOpen(Stream stream, out PdfiumDocument? document)
    {
        document = null;
        if (stream.Length > PdfDocument.MaxSourceBytes)
        {
            return "LIMIT_EXCEEDED";
        }

        var length = (int)stream.Length;
        var buffer = (byte*)NativeMemory.Alloc((nuint)Math.Max(length, 1));
        try
        {
            stream.ReadExactly(new Span<byte>(buffer, length));
            lock (Pdfium.Gate)
            {
                if (!_initialized)
                {
                    Pdfium.InitLibrary();
                    _initialized = true;
                }

                // Sem senha: abre o PDF que só tem senha de dono. Com senha de usuário, cai em PASSWORD (FR-24 vem depois).
                var handle = Pdfium.LoadMemDocument(buffer, length, null);
                if (handle == 0)
                {
                    var code = Pdfium.GetLastError() == Pdfium.ErrorPassword ? "PASSWORD" : "CORRUPTED";
                    NativeMemory.Free(buffer);
                    return code;
                }

                var pages = Pdfium.GetPageCount(handle);
                if (pages is <= 0 or > PdfDocument.MaxPages)
                {
                    Pdfium.CloseDocument(handle);
                    NativeMemory.Free(buffer);
                    return pages > PdfDocument.MaxPages ? "LIMIT_EXCEEDED" : "CORRUPTED";
                }

                document = new PdfiumDocument(handle, buffer, pages);
                return null;
            }
        }
        catch
        {
            NativeMemory.Free(buffer);
            throw;
        }
    }

    /// <summary>Tamanho da página em pontos (1/72 de polegada), ou null se o PDFium não conseguir abri-la.</summary>
    public (double Width, double Height)? PageSize(int index)
    {
        lock (Pdfium.Gate)
        {
            var page = Pdfium.LoadPage(_document, index);
            if (page == 0)
            {
                return null;
            }

            try
            {
                return (Pdfium.GetPageWidth(page), Pdfium.GetPageHeight(page));
            }
            finally
            {
                Pdfium.ClosePage(page);
            }
        }
    }

    /// <summary>
    /// Desenha a página sobre fundo branco no buffer BGRx de <paramref name="width"/> × <paramref name="height"/>
    /// pixels (4 bytes por pixel). O chamador aloca e libera o buffer.
    /// </summary>
    public bool Render(int index, byte* pixels, int width, int height)
    {
        lock (Pdfium.Gate)
        {
            var page = Pdfium.LoadPage(_document, index);
            if (page == 0)
            {
                return false;
            }

            var bitmap = Pdfium.BitmapCreateEx(width, height, Pdfium.BitmapBgrx, (nint)pixels, width * 4);
            try
            {
                if (bitmap == 0)
                {
                    return false;
                }

                Pdfium.BitmapFillRect(bitmap, 0, 0, width, height, 0xFFFFFFFF);
                Pdfium.RenderPageBitmap(bitmap, page, 0, 0, width, height, 0, 0);
                return true;
            }
            finally
            {
                if (bitmap != 0)
                {
                    Pdfium.BitmapDestroy(bitmap);
                }

                Pdfium.ClosePage(page);
            }
        }
    }

    /// <summary>O texto da página, como o PDFium o devolve; vazio se a página não tem texto.</summary>
    public string Text(int index)
    {
        lock (Pdfium.Gate)
        {
            var page = Pdfium.LoadPage(_document, index);
            if (page == 0)
            {
                return "";
            }

            try
            {
                var text = Pdfium.TextLoadPage(page);
                if (text == 0)
                {
                    return "";
                }

                try
                {
                    var count = Pdfium.TextCountChars(text);
                    if (count <= 0)
                    {
                        return "";
                    }

                    var characters = new ushort[count + 1];
                    fixed (ushort* result = characters)
                    {
                        var written = Pdfium.TextGetText(text, 0, count, result);
                        return written <= 1 ? "" : new string((char*)result, 0, written - 1);
                    }
                }
                finally
                {
                    Pdfium.TextClosePage(text);
                }
            }
            finally
            {
                Pdfium.ClosePage(page);
            }
        }
    }

    public void Dispose()
    {
        if (_document != 0)
        {
            lock (Pdfium.Gate)
            {
                Pdfium.CloseDocument(_document);
            }

            _document = 0;
        }

        if (_source != null)
        {
            NativeMemory.Free(_source);
            _source = null;
        }
    }
}
