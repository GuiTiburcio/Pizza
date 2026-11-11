using System.Reflection;
using System.Runtime.InteropServices;
using Pizza.App;
using Pizza.Core.Conversions;
using Pizza.Core.Gesture;
using Pizza.Core.Visual;
using static Pizza.App.Interop.GdiPlus;

namespace Pizza.App.Tests;

/// <summary>TST-01 / WF-1 / NFR-06: fonte real do Windows, sem janela nem captura de tela.</summary>
public sealed unsafe partial class SliceLabelTests
{
    [Fact]
    public void Texto_completo_cabe_no_contorno_em_todas_as_orientacoes_sem_reduzir_fonte()
    {
        DiscRenderer.Start();
        Assert.Equal(0, GdipCreateBitmapFromScan0(800, 1100, 0, FormatArgb, 0, out var bitmap));
        Assert.Equal(0, GdipGetImageGraphicsContext(bitmap, out var g));
        try
        {
            foreach (var converter in ConverterCatalog.All)
            foreach (var hover in new[] { 0.0, 0.5, 1.0 })
            for (var angle = -180; angle < 180; angle += 15)
            {
                var labels = DiscRenderer.LayoutLabels(g, converter, angle, hover, hover, pulse: false);
                Assert.InRange(labels.CaptionLines, 1, 2);
                var path = DiscRenderer.SlicePath(SliceGeometry.Build(angle, hover, DiscLayout.CenterX, DiscLayout.CenterY));
                try
                {
                    foreach (var bounds in new[] { labels.Title, labels.CaptionInk })
                    {
                        // Inclui os meios das arestas: a borda interna da fatia é côncava.
                        for (var point = 0; point <= 8; point++)
                        {
                            var x = bounds.X + (bounds.Width * point / 8);
                            var y = bounds.Y + (bounds.Height * point / 8);
                            Inside(path, g, x, bounds.Y, converter.Id, angle, hover);
                            Inside(path, g, x, bounds.Y + bounds.Height, converter.Id, angle, hover);
                            Inside(path, g, bounds.X, y, converter.Id, angle, hover);
                            Inside(path, g, bounds.X + bounds.Width, y, converter.Id, angle, hover);
                        }
                    }
                }
                finally { GdipDeletePath(path); }
            }
        }
        finally
        {
            GdipDeleteGraphics(g);
            GdipDisposeImage(bitmap);
        }
    }

    private static void Inside(nint path, nint g, float x, float y, string id, int angle, double hover)
    {
        Assert.Equal(0, IsVisible(path, x, y, g, out var visible));
        Assert.True(visible != 0, $"{id}, ângulo {angle}, hover {hover}, ponto ({x:F1}, {y:F1})");
    }

    [LibraryImport("gdiplus.dll", EntryPoint = "GdipIsVisiblePathPoint")]
    private static partial int IsVisible(nint path, float x, float y, nint g, out int visible);
}
