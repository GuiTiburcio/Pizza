using System.Reflection;
using Pizza.App;
using Pizza.App.Interop;
using Pizza.Core.Conversions;
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
        Assert.False(Overlay.OnDrop(Files(droppedPath), DiscLayout.CenterX, DiscLayout.CenterY - 130));
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
        Set("_files", Files("entrada.jpg", "outra.jpg"));
        Assert.Null(TargetAt(0, -130));
    }

    [Fact]
    public void Varios_pdfs_so_acendem_o_juntar()
    {
        // FR-30 com a emenda da ADR-033 (prancha 6A-1).
        PreparePdf("escritura.pdf", "anexo.pdf", "recibo.pdf");

        Assert.Equal(0, TargetAt(SliceCenter(0).X - DiscLayout.CenterX, SliceCenter(0).Y - DiscLayout.CenterY));
        Assert.Null(TargetAt(SliceCenter(1).X - DiscLayout.CenterX, SliceCenter(1).Y - DiscLayout.CenterY));
        Assert.Null(TargetAt(SliceCenter(2).X - DiscLayout.CenterX, SliceCenter(2).Y - DiscLayout.CenterY));
    }

    [Fact]
    public void Um_pdf_nao_acende_o_juntar_mas_acende_o_resto()
    {
        PreparePdf("escritura.pdf");

        Assert.Null(TargetAt(SliceCenter(0).X - DiscLayout.CenterX, SliceCenter(0).Y - DiscLayout.CenterY));
        Assert.Equal(1, TargetAt(SliceCenter(1).X - DiscLayout.CenterX, SliceCenter(1).Y - DiscLayout.CenterY));
    }

    [Fact]
    public void Soltar_varios_pdfs_no_juntar_leva_todos_na_ordem()
    {
        PreparePdf("escritura.pdf", "anexo.pdf", "recibo.pdf");
        var (x, y) = SliceCenter(0);

        Assert.True(Overlay.OnDrop(Files("escritura.pdf", "anexo.pdf", "recibo.pdf"), x, y));
        Assert.Equal("pdf.merge", Dropped!.Value.Converter.Id);
        Assert.Equal(["escritura.pdf", "anexo.pdf", "recibo.pdf"], Dropped!.Value.Paths);
    }

    [Fact]
    public void Soltura_com_ordem_diferente_da_entrada_e_recusada()
    {
        // SEC-06: CF_HDROP relido na soltura tem de ser o mesmo arraste.
        PreparePdf("escritura.pdf", "anexo.pdf");
        var (x, y) = SliceCenter(0);

        Assert.False(Overlay.OnDrop(Files("anexo.pdf", "escritura.pdf"), x, y));
    }

    [Fact]
    public void Pdf_misturado_com_imagem_nao_acende_nada()
    {
        // FR-31 (tipos misturados) é da v1.x.
        PreparePdf("escritura.pdf", "foto.jpg");

        Assert.Null(TargetAt(SliceCenter(0).X - DiscLayout.CenterX, SliceCenter(0).Y - DiscLayout.CenterY));
        Assert.False(Overlay.OnDragOver(SliceCenter(0).X, SliceCenter(0).Y));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4000, -3000)]
    [InlineData(-4000, 3000)]
    public void Soltar_em_qualquer_posicao_prepara_a_escolha(int x, int y)
    {
        Prepare();
        Assert.True(Overlay.OnDrop(Files("entrada.jpg"), x, y, selectSlice: false));
        Assert.Equal(GestureState.Choosing, Session.State);
    }

    [Fact]
    public void Clique_numa_fatia_durante_a_escolha_escolhe_o_formato()
    {
        // ADR-025: os dois botões escolhem a fatia; o direito é o caminho para as opções.
        PrepareChoice();

        Assert.True(ChooseAt(0, -130));
        Assert.Equal(GestureState.Idle, Session.State);
        Assert.Equal(0, (int?)typeof(Overlay).GetField("_pulseSlot", PrivateStatic)!.GetValue(null));
    }

    [Fact]
    public void Clique_fora_das_fatias_durante_a_escolha_nao_faz_nada()
    {
        PrepareChoice();

        Assert.False(ChooseAt(0, 0));
        Assert.Equal(GestureState.Choosing, Session.State);
    }

    [Fact]
    public void Clique_numa_fatia_de_outro_tipo_aponta_a_aba_e_mantem_a_escolha()
    {
        // ADR-036: a imagem na aba PDF vê Juntar, Dividir e Girar apagados; escolher um não converte.
        PrepareChoice();
        var pdfPage = OpenPage("entrada.jpg", ConverterCatalog.PdfTab);

        Assert.False(ChooseAt(0, -130));
        Assert.Equal(GestureState.Choosing, Session.State);
        Assert.Null(Dropped);
        Assert.Equal(pdfPage, (MenuPage?)Get("_hintPage"));
        Assert.Equal(0, (int)Get("_hintSlot")!);
        Assert.Equal("Experimente a aba Imagem", RadialMenu.Hint("entrada.jpg"));
    }

    [Fact]
    public void Soltar_numa_fatia_de_outro_tipo_deixa_o_menu_aberto_com_o_aviso()
    {
        Prepare();
        var pdfPage = OpenPage("entrada.jpg", ConverterCatalog.PdfTab);
        var (x, y) = SliceCenter(1);

        Assert.Null(TargetAt(x - DiscLayout.CenterX, y - DiscLayout.CenterY));
        Overlay.OnDrop(Files("entrada.jpg"), x, y);

        Assert.Equal(GestureState.Choosing, Session.State);
        Assert.Null(Dropped);
        Assert.Equal(pdfPage, (MenuPage?)Get("_hintPage"));
        Assert.Equal(1, (int)Get("_hintSlot")!);
    }

    [Fact]
    public void Clique_numa_fatia_que_serve_nao_mostra_aviso()
    {
        PrepareChoice();

        Assert.True(ChooseAt(0, -130));
        Assert.Null((MenuPage?)Get("_hintPage"));
    }

    [Fact]
    public void Prazo_sem_interacao_cancela_a_escolha()
    {
        // ADR-025: 10 s sem interação fecham o menu e descartam a cópia da tela.
        PrepareChoice();

        typeof(Overlay).GetMethod("OnTimer", PrivateStatic)!.Invoke(null, [ChoiceTimer]);

        Assert.Equal(GestureState.Idle, Session.State);
        Assert.False(Overlay.IsVisible);
    }

    [Theory]
    [InlineData(Native.WM_LBUTTONDOWN)]
    [InlineData(Native.WM_LBUTTONUP)]
    [InlineData(Native.WM_RBUTTONDOWN)]
    [InlineData(Native.WM_RBUTTONUP)]
    public void Botao_no_miolo_reinicia_prazo_e_timer_antigo_nao_fecha_a_escolha(uint message)
    {
        PrepareChoice();
        var before = Environment.TickCount64;
        typeof(Overlay).GetMethod("OnChoiceButton", PrivateStatic)!.Invoke(null,
            [message, DiscLayout.CenterX, DiscLayout.CenterY]);

        var deadline = (long)typeof(Overlay).GetField("_choiceDeadline", PrivateStatic)!.GetValue(null)!;
        Assert.True(deadline >= before + 10_000);
        // WM_TIMER pode estar na fila quando uma interação renova o prazo.
        typeof(Overlay).GetMethod("OnTimer", PrivateStatic)!.Invoke(null, [ChoiceTimer]);
        Assert.Equal(GestureState.Choosing, Session.State);
    }

    // ADR-027: entrada.jpg mostra PNG, WEBP, AVIF, PDF, TIFF e ICO; a fatia 1 é o WEBP.
    [Fact]
    public void Soltar_num_nivel_converte_com_esse_nivel()
    {
        Prepare();
        HoverSlice(1);

        Assert.True(Overlay.OnDrop(Files("entrada.jpg"), LevelX(0, 4), LevelY));

        Assert.Equal("image.webp", Dropped!.Value.Converter.Id);
        Assert.Equal("95", Dropped!.Value.Options!["quality"]);
        Assert.Equal(1, (int?)Get("_pulseSlot"));
    }

    [Fact]
    public void Soltar_na_fatia_converte_com_o_nivel_marcado_sem_abrir_o_painel()
    {
        Prepare();
        var (x, y) = SliceCenter(1);
        HoverSlice(1);

        Assert.True(Overlay.OnDrop(Files("entrada.jpg"), x, y));

        Assert.Equal("75", Dropped!.Value.Options!["quality"]);
    }

    [Fact]
    public void Roda_sobre_a_faixa_troca_o_nivel_e_fora_dela_troca_a_aba()
    {
        Prepare();
        HoverSlice(1);
        Wheel(-120, LevelX(1, 4), LevelY);
        Assert.Equal(0, Carousel.Target);

        var (x, y) = SliceCenter(1);
        Assert.True(Overlay.OnDrop(Files("entrada.jpg"), x, y));
        Assert.Equal("60", Dropped!.Value.Options!["quality"]);

        Prepare();
        Wheel(-120, DiscLayout.CenterX, DiscLayout.CenterY);
        Assert.Equal(1, Carousel.Target);
    }

    [Fact]
    public void Passar_de_raspao_por_outra_fatia_nao_troca_a_faixa()
    {
        // A caminho da faixa, o cursor cruza a fatia de cima; só uma parada nela troca os níveis.
        Prepare();
        HoverSlice(1);
        HoverSlice(0);
        Assert.Equal("image.webp", Strip.Converter!.Id);

        typeof(Overlay).GetMethod("OnTimer", PrivateStatic)!.Invoke(null, [StripTimer]);
        Assert.Equal("image.png", Strip.Converter!.Id);
    }

    [Fact]
    public void Bolha_dos_niveis_nasce_no_padrao_e_desliza_ate_o_nivel_da_roda()
    {
        // ADR-027: a bolha marca o nível escolhido e desliza na mola de 240 ms.
        Prepare();
        HoverSlice(1);
        Assert.Equal(2, LevelGlide.Position);

        Wheel(-120, LevelX(1, 4), LevelY);
        Assert.Equal(3, LevelGlide.Goal);
        Assert.Equal(2, LevelGlide.Position);
    }

    [Fact]
    public void Bolha_das_fatias_nasce_na_primeira_e_desliza_para_a_proxima()
    {
        Prepare();
        HoverSlice(1);
        Assert.Equal(1, SliceGlide.Position);

        HoverSlice(2);
        Assert.Equal(2, SliceGlide.Goal);
        Assert.Equal(1, SliceGlide.Position);
    }

    [Fact]
    public void Sem_animacao_a_bolha_vai_direto()
    {
        Prepare();
        Set("_motion", MotionStyle.Instant);
        HoverSlice(1);
        HoverSlice(2);

        Assert.Equal(2, SliceGlide.Position);
    }

    [Theory]
    [InlineData(Native.WM_LBUTTONDOWN, Native.WM_LBUTTONUP, false)]
    [InlineData(Native.WM_RBUTTONDOWN, Native.WM_RBUTTONUP, true)]
    public void Botao_direito_na_fatia_abre_o_painel_e_o_esquerdo_converte(uint down, uint up, bool panel)
    {
        PrepareChoice();
        var (x, y) = SliceCenter(1);
        var choose = typeof(Overlay).GetMethod("OnChoiceButton", PrivateStatic)!;
        choose.Invoke(null, [down, x, y]);
        choose.Invoke(null, [up, x, y]);

        Assert.Equal("image.webp", Dropped!.Value.Converter.Id);
        Assert.Equal(panel, Dropped!.Value.Options is null);
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

    [Fact]
    public void Timeout_antigo_apos_nova_soltura_respeita_o_novo_prazo()
    {
        Prepare();
        typeof(Overlay).GetMethod("OnButtonReleased", PrivateStatic)!.Invoke(null, null);
        Assert.True((long)typeof(Overlay).GetField("_dropDeadline", PrivateStatic)!.GetValue(null)! > 0);
        Set("_dropDeadline", Environment.TickCount64 + 60_000);
        var timer = Convert.ToUInt32(typeof(Overlay).GetField("TimerAfterRelease", PrivateStatic)!.GetRawConstantValue());
        typeof(Overlay).GetMethod("OnTimer", PrivateStatic)!.Invoke(null, [(nuint)timer]);

        Assert.Equal(GestureState.Menu, Session.State);
        Assert.True(Overlay.IsVisible);
        // O prazo realmente vencido ainda fecha o gesto sem drop confirmado.
        Set("_dropDeadline", 0L);
        typeof(Overlay).GetMethod("OnTimer", PrivateStatic)!.Invoke(null, [(nuint)timer]);
        Assert.Equal(GestureState.Idle, Session.State);
        Assert.False(Overlay.IsVisible);
    }

    private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static CardTransition[] Hover => (CardTransition[])typeof(Overlay).GetField("Hover", PrivateStatic)!.GetValue(null)!;

    private static void Prepare()
    {
        Set("_files", Files("entrada.jpg"));
        Set("_left", 0);
        Set("_top", 0);
        Set("_scale", 1.0);
        Set("_hoverSlot", null);
        Set("_pulseSlot", null);
        Set("_dropped", null);
        Set("_motion", MotionStyle.Slide);
        Set("_choiceDeadline", 0L);
        Set("_clickArmed", false);
        Set("_levelHover", null);
        Set("_hintPage", null);
        Set("_pages", RadialMenu.Pages("entrada.jpg"));
        Strip.Clear();
        LevelGlide.Snap(0);
        SliceGlide.Snap(0);
        typeof(Overlay).GetProperty(nameof(Overlay.IsVisible))!.SetValue(null, true);
        ((TabCarousel)typeof(Overlay).GetField("Carousel", PrivateStatic)!.GetValue(null)!).Reset();
        var session = (GestureSession)typeof(Overlay).GetField("Session", PrivateStatic)!.GetValue(null)!;
        session.Begin();
        session.OnDragEnter(hasFiles: true);
        foreach (var hover in Hover) hover.Snap(0);
    }

    private static nuint ChoiceTimer => Convert.ToUInt32(typeof(Overlay).GetField("TimerChoice", PrivateStatic)!.GetRawConstantValue());

    private static void PrepareChoice()
    {
        Prepare();
        Assert.True(Overlay.OnDrop(Files("entrada.jpg"), 0, 0, selectSlice: false));
    }

    private static bool ChooseAt(int x, int y) =>
        (bool)typeof(Overlay).GetMethod("ChooseAt", PrivateStatic)!.Invoke(null, [DiscLayout.CenterX + x, DiscLayout.CenterY + y, false])!;

    private static TabCarousel Carousel => (TabCarousel)Get("Carousel")!;
    private static QuickOptionsState Strip => (QuickOptionsState)Get("Strip")!;
    private static GlideSpring LevelGlide => (GlideSpring)Get("LevelGlide")!;
    private static GlideSpring SliceGlide => (GlideSpring)Get("SliceGlide")!;
    private static nuint StripTimer => Convert.ToUInt32(typeof(Overlay).GetField("TimerStrip", PrivateStatic)!.GetRawConstantValue());
    private static int LevelY => DiscLayout.LevelTop + (DiscLayout.LevelHeight / 2);

    private static (IReadOnlyList<string> Paths, ConverterInfo Converter, IReadOnlyDictionary<string, string>? Options)? Dropped =>
        ((IReadOnlyList<string>, ConverterInfo, IReadOnlyDictionary<string, string>?)?)Get("_dropped");

    private static DraggedFiles Files(params string[] paths) => new(paths);

    /// <summary>Menu aberto na aba PDF, como o FR-03 faz com um PDF: Juntar, Dividir e Girar nas fatias 0, 1 e 2.</summary>
    private static void PreparePdf(params string[] paths)
    {
        Prepare();
        Set("_files", Files(paths));
        var pages = RadialMenu.Pages(paths[0]);
        Set("_pages", pages);
        Carousel.Reset(pages.Count, RadialMenu.FirstPage(paths[0]));
    }

    /// <summary>Gira o menu até a primeira volta da aba pedida, como a roda faz.</summary>
    private static MenuPage OpenPage(string path, int tab)
    {
        var pages = RadialMenu.Pages(path);
        var index = pages.ToList().FindIndex(page => page.Tab == tab);
        Set("_pages", pages);
        Carousel.Reset(pages.Count, index);
        return pages[index];
    }

    private static int LevelX(int level, int count)
    {
        var (left, right) = DiscLayout.LevelSpan(level, count);
        return (int)((left + right) / 2);
    }

    private static (int X, int Y) SliceCenter(int slot)
    {
        var angle = RadialMenu.SlotAngle(slot) * Math.PI / 180;
        return (DiscLayout.CenterX + (int)(130 * Math.Cos(angle)), DiscLayout.CenterY + (int)(130 * Math.Sin(angle)));
    }

    private static void HoverSlice(int slot)
    {
        var (x, y) = SliceCenter(slot);
        Assert.True(Overlay.OnDragOver(x, y));
    }

    private static void Wheel(int delta, int x, int y) =>
        typeof(Overlay).GetMethod("OnWheel", PrivateStatic)!.Invoke(null, [delta, x, y]);

    private static object? Get(string name) => typeof(Overlay).GetField(name, PrivateStatic)!.GetValue(null);

    private static int? TargetAt(int x, int y) =>
        (int?)typeof(Overlay).GetMethod("TargetSlot", PrivateStatic)!.Invoke(null, [DiscLayout.CenterX + x, DiscLayout.CenterY + y]);

    private static void Set(string name, object? value) => typeof(Overlay).GetField(name, PrivateStatic)!.SetValue(null, value);
}

