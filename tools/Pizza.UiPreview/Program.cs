using System.Runtime.InteropServices;
using Pizza.Core.Conversions;
using Pizza.Core.Notifications;
using static Pizza.App.Interop.GdiPlus;

namespace Pizza.App;

/// <summary>Prévia determinística pelo desenho de produção, sem janela ou captura da tela.</summary>
internal static partial class DiscRenderer
{
    public static void Main(string[] args)
    {
        var folder = args.Length == 1 ? args[0] : "artifacts/ui";
        Directory.CreateDirectory(folder);
        Start();
        var history = new ConversionHistory();
        history.Add(1, "gravacao.wav", ConverterCatalog.Find("audio.mp3")!, new Dictionary<string, string> { ["bitrate"] = "320" });
        history.OfferBitrate(1, 192);
        var files = new FilesPanelState(history);
        foreach (var dark in new[] { false, true })
        {
            _palette = dark ? Dark : Light;
            foreach (var dpi in new[] { 96, 144, 192 })
            {
                var theme = dark ? "escuro" : "claro";
                Render(Path.Combine(folder, $"bitrate-{theme}-{dpi}.png"), 380, 148, dpi,
                    g => DrawCard(g, new CardFrame(NotificationKind.Confirmation, "Confirmar qualidade", history.Find(1)!.Message!, 0, 0, "", false), 380, 148));
                Render(Path.Combine(folder, $"arquivos-{theme}-{dpi}.png"), FilesPanelState.Width, files.Height, dpi,
                    g => DrawFiles(g, files, history.Items, FilesPanelState.Width, files.Height));
            }
        }
    }

    private static unsafe void Render(string path, int width, int height, int dpi, Action<nint> draw)
    {
        var scale = dpi / 96f;
        _scale = scale;
        var pixels = new byte[(int)(width * scale) * (int)(height * scale) * 4];
        fixed (byte* bits = pixels)
        {
            Check(GdipCreateBitmapFromScan0((int)(width * scale), (int)(height * scale), (int)(width * scale) * 4, FormatPargb, (nint)bits, out var bitmap));
            try
            {
                Check(GdipGetImageGraphicsContext(bitmap, out var graphics));
                try
                {
                    Check(GdipSetSmoothingMode(graphics, SmoothingAntiAlias));
                    Check(GdipSetTextRenderingHint(graphics, TextAntiAlias));
                    Check(GdipSetPixelOffsetMode(graphics, PixelOffsetHighQuality));
                    Check(GdipGraphicsClear(graphics, _palette == Dark ? 0xFF303746 : 0xFFE5EAF0));
                    Check(GdipScaleWorldTransform(graphics, scale, scale, 0));
                    draw(graphics);
                }
                finally
                {
                    GdipDeleteGraphics(graphics);
                }

                var png = new Guid("557cf406-1a04-11d3-9a73-0000f81ef32e");
                Check(GdipSaveImageToFile(bitmap, Path.GetFullPath(path), &png, 0));
            }
            finally
            {
                GdipDisposeImage(bitmap);
            }
        }
    }

    private static void Check(int status)
    {
        if (status != 0)
        {
            throw new InvalidOperationException($"GDI+ retornou {status}.");
        }
    }

    [LibraryImport("gdiplus.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static unsafe partial int GdipSaveImageToFile(nint image, string path, Guid* encoder, nint parameters);
}
