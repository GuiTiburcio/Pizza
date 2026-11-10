using Pizza.Core.Conversions;

namespace Pizza.Core.Tests.Conversions;

/// <summary>NFR-08: painel PNG em área útil de 640 px físicos, a 200%.</summary>
public class PanelViewportTests
{
    [Fact]
    public void Em_200_porcento_botoes_podem_ser_revelados_sem_sair_da_tela()
    {
        var panel = new OptionsPanelState(ConverterCatalog.Find("image.png")!);
        var viewport = new PanelViewport(panel.Height, 640 * 96 / 192);

        Assert.True(viewport.CanScroll);
        Assert.True(viewport.Height * 192 / 96 <= 640);
        foreach (var target in panel.Targets)
        {
            viewport.Reveal(target.Bounds);
            Assert.InRange(target.Bounds.Top - viewport.Offset, 0, viewport.ContentHeight);
            Assert.InRange(target.Bounds.Bottom - viewport.Offset, 0, viewport.ContentHeight);
            var y = target.Bounds.Bottom - 1 - viewport.Offset;
            Assert.Equal(target, panel.TargetAt(target.Bounds.Left + 1, y + viewport.Offset));
        }

        viewport.Reveal(panel.Targets[0].Bounds);
        Assert.True(panel.Targets[0].Bounds.Top >= viewport.Offset);
    }

    [Fact]
    public void Rolagem_por_mouse_alcanca_converter_e_respeita_os_limites()
    {
        var panel = new OptionsPanelState(ConverterCatalog.Find("image.png")!);
        var viewport = new PanelViewport(panel.Height, 320);
        viewport.Scroll(10_000);
        var convert = panel.Targets.Single(t => t.Kind == PanelTargetKind.Convert);
        Assert.InRange(convert.Bounds.Bottom - viewport.Offset, 0, viewport.ContentHeight);
        var last = viewport.Offset;
        viewport.Scroll(44);
        Assert.Equal(last, viewport.Offset);
        viewport.Scroll(-10_000);
        Assert.Equal(0, viewport.Offset);
    }

    [Fact]
    public void Area_suficiente_dispensa_rolagem_e_rodape_adicional()
    {
        var viewport = new PanelViewport(400, 600);
        viewport.Scroll(44);
        Assert.False(viewport.CanScroll);
        Assert.Equal(400, viewport.Height);
        Assert.Equal(400, viewport.ContentHeight);
        Assert.Equal(0, viewport.Offset);
    }
}
