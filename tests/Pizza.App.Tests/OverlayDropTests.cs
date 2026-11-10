using System.Reflection;
using Pizza.App;
using Pizza.App.Interop;
using Pizza.Core.Gesture;
using Pizza.Core.Notifications;
using Pizza.Core.Visual;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Pizza.App.Tests;

// TST-01 / FR-05 / WF-2; o drop também deve preservar SEC-05, SEC-06 e SEC-31.
// A geometria de repouso é mantida como tolerância para estabilizar o hover.
public sealed class OverlayDropTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [Trait("Tipo", "Achado")]
    public void Hover_preserva_tolerancia_na_borda_interna(int slot)
    {
        var angle = RadialMenu.SlotAngle(slot) * Math.PI / 180;
        Assert.Equal(slot, RadialMenu.SlotAt(94 * Math.Cos(angle), 94 * Math.Sin(angle), 0, slot, 1));
    }

    [Fact]
    [Trait("Tipo", "Achado")]
    public void Alvo_considera_a_fatia_anterior_ainda_encolhendo()
    {
        Prepare();
        Hover[0].Snap(0.5);
        Set("_hoverSlot", 1);

        Assert.Equal(0, TargetAt(0, -173));
    }

    [Theory]
    [InlineData("nao-conversivel.txt")]
    [InlineData("sem-extensao")]
    [Trait("Tipo", "Achado")]
    public void Drop_com_dados_diferentes_da_entrada_e_recusado_sem_excecao(string droppedPath)
    {
        Prepare();
        Assert.Equal(0, TargetAt(0, -130));

        // A entrada continha JPG; CF_HDROP é lido novamente na soltura.
        // Nenhum arquivo é aberto. Os HWNDs ficam em zero; não são criadas janelas.
        Assert.False(Overlay.OnDrop(new DraggedFiles(droppedPath, 1), DiscLayout.CenterX, DiscLayout.CenterY - 130));
    }

    [Fact]
    [Trait("Tipo", "Controle")]
    public void Alvo_em_repouso_aceita_o_centro_da_fatia()
    {
        Prepare();
        Assert.Equal(0, TargetAt(0, -130));
    }

    [Fact]
    [Trait("Tipo", "Controle")]
    public void Alvo_em_hover_aceita_a_area_nova_da_fatia_atual()
    {
        Prepare();
        Hover[0].Snap(1);
        Set("_hoverSlot", 0);
        Assert.Equal(0, TargetAt(0, -178));
    }

    [Fact]
    [Trait("Tipo", "Controle")]
    public void Fora_da_fatia_em_repouso_nao_ha_alvo()
    {
        Prepare();
        Assert.Null(TargetAt(0, -173));
    }

    [Fact]
    [Trait("Tipo", "Controle")]
    public void Arraste_com_varios_arquivos_nao_aceita_drop()
    {
        Prepare();
        Set("_files", new DraggedFiles("entrada.jpg", 2));
        Assert.Null(TargetAt(0, -130));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4000, -3000)]
    [InlineData(-4000, 3000)]
    public void Soltar_em_qualquer_posicao_prepara_a_escolha(int x, int y)
    {
        Prepare();
        Assert.True(Overlay.OnDrop(new DraggedFiles("entrada.jpg", 1), x, y, selectSlice: false));
        Assert.Equal(GestureState.Choosing, Session.State);
    }

    [Fact]
    public void Efeito_sem_copy_nao_e_aceito()
    {
        Prepare();
        var effect = 2;
        new DropTarget(false).DragOver(0, default, ref effect);
        Assert.Equal(0, effect);
    }

    [Fact]
    public void Camada_aceita_copy_para_preparar_fora_do_disco()
    {
        Prepare();
        var effect = 1;
        new DropTarget(false).DragOver(0, new Native.POINT { X = -4000, Y = -3000 }, ref effect);
        Assert.Equal(1, effect);
    }

    private static GestureSession Session => (GestureSession)typeof(Overlay).GetField("Session", PrivateStatic)!.GetValue(null)!;

    private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static CardTransition[] Hover => (CardTransition[])typeof(Overlay).GetField("Hover", PrivateStatic)!.GetValue(null)!;

    private static void Prepare()
    {
        Set("_files", new DraggedFiles("entrada.jpg", 1));
        Set("_left", 0);
        Set("_top", 0);
        Set("_scale", 1.0);
        Set("_hoverSlot", null);
        Set("_pulseSlot", null);
        Set("_dropped", null);
        Set("_motion", MotionStyle.Slide);
        typeof(Overlay).GetProperty(nameof(Overlay.IsVisible))!.SetValue(null, true);
        ((TabCarousel)typeof(Overlay).GetField("Carousel", PrivateStatic)!.GetValue(null)!).Reset();
        var session = (GestureSession)typeof(Overlay).GetField("Session", PrivateStatic)!.GetValue(null)!;
        session.Begin();
        session.OnDragEnter(hasFiles: true);
        foreach (var hover in Hover) hover.Snap(0);
    }

    private static int? TargetAt(int x, int y) =>
        (int?)typeof(Overlay).GetMethod("TargetSlot", PrivateStatic)!.Invoke(null, [DiscLayout.CenterX + x, DiscLayout.CenterY + y]);

    private static void Set(string name, object? value) => typeof(Overlay).GetField(name, PrivateStatic)!.SetValue(null, value);
}
