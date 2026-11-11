using Pizza.Core.Conversions;
using Pizza.Core.Notifications;

namespace Pizza.Core.Tests.Notifications;

/// <summary>FR-44 e NFR-06: lista rolável e uso completo pelo teclado. ADR-028, prancha 4c-3: só a linha em foco abre.</summary>
public class FilesPanelStateTests
{
    private const int Extra = FilesPanelState.OpenHeight - FilesPanelState.RowHeight;

    private static ConversionHistory History(params HistoryState[] states)
    {
        var history = new ConversionHistory();
        var converter = ConverterCatalog.Find("image.png")!;
        for (var index = 0; index < states.Length; index++)
        {
            long id = index + 1;
            history.Add(id, $"C:\\a\\{id}.jpg", converter, new Dictionary<string, string>());
            if (states[index] == HistoryState.Done)
            {
                history.Complete(id, $"C:\\a\\{id}.png");
            }
            else if (states[index] == HistoryState.Failed)
            {
                history.Fail(id, "motivo");
            }
        }

        return history;
    }

    private static HistoryState[] Done(int count) => Enumerable.Repeat(HistoryState.Done, count).ToArray();

    private static int Y(FilesPanelState panel, int row, int offset) => FilesPanelState.HeaderHeight + panel.RowTop(row) - panel.Scroll + offset;

    [Fact]
    public void Lista_vazia_tem_area_para_o_aviso()
    {
        var panel = new FilesPanelState(History());

        Assert.Equal(FilesPanelState.HeaderHeight + FilesPanelState.EmptyHeight + FilesPanelState.BottomPadding, panel.Height);
        Assert.Equal(-1, panel.OpenRow);
        Assert.Null(panel.Press(PanelKey.Enter));
    }

    [Fact]
    public void Altura_cresce_com_a_lista_ate_o_limite()
    {
        var one = new FilesPanelState(History(Done(1)));
        var many = new FilesPanelState(History(Done(30)));

        Assert.Equal(FilesPanelState.HeaderHeight + FilesPanelState.OpenHeight + FilesPanelState.BottomPadding, one.Height);
        Assert.Equal(FilesPanelState.HeaderHeight + (5 * FilesPanelState.RowHeight) + Extra + FilesPanelState.BottomPadding, many.Height);
    }

    [Fact]
    public void Lista_so_em_andamento_fica_compacta()
    {
        var panel = new FilesPanelState(History(HistoryState.Running, HistoryState.Running));

        Assert.Equal(-1, panel.OpenRow);
        Assert.Equal(2 * FilesPanelState.RowHeight, panel.ViewportHeight);
    }

    [Fact]
    public void So_a_linha_em_foco_fica_aberta()
    {
        var panel = new FilesPanelState(History(HistoryState.Done, HistoryState.Running, HistoryState.Failed));

        Assert.Equal(0, panel.OpenRow);
        Assert.Equal(FilesPanelState.OpenHeight, panel.RowHeightAt(0));
        Assert.Equal(FilesPanelState.RowHeight, panel.RowHeightAt(2));
        Assert.Equal(FilesPanelState.OpenHeight + FilesPanelState.RowHeight, panel.RowTop(2));

        panel.Press(PanelKey.Tab);
        panel.Press(PanelKey.Tab);

        Assert.Equal(2, panel.OpenRow);
        Assert.Equal(FilesPanelState.RowHeight, panel.RowHeightAt(0));
        Assert.Equal(2 * FilesPanelState.RowHeight, panel.RowTop(2));
    }

    [Fact]
    public void Altura_nao_muda_quando_o_foco_anda()
    {
        var panel = new FilesPanelState(History(HistoryState.Done, HistoryState.Failed, HistoryState.Done));
        var height = panel.Height;

        for (var i = 0; i < 5; i++)
        {
            panel.Press(PanelKey.Tab);
            Assert.Equal(height, panel.Height);
        }
    }

    [Fact]
    public void Roda_do_mouse_rola_sem_passar_dos_limites()
    {
        var panel = new FilesPanelState(History(Done(8)));

        panel.Wheel(120);
        Assert.Equal(0, panel.Scroll);

        panel.Wheel(-120);
        Assert.True(panel.Scroll > 0);

        for (var i = 0; i < 50; i++)
        {
            panel.Wheel(-120);
        }

        Assert.Equal(3 * FilesPanelState.RowHeight, panel.Scroll);
        Assert.Equal(panel.ContentHeight - panel.ViewportHeight, panel.MaxScroll);
    }

    [Fact]
    public void Lista_que_cabe_nao_rola()
    {
        var panel = new FilesPanelState(History(Done(5)));

        panel.Wheel(-120);

        Assert.Equal(0, panel.Scroll);
    }

    [Fact]
    public void Enter_executa_o_botao_em_foco()
    {
        var panel = new FilesPanelState(History(HistoryState.Done));

        Assert.Equal((1L, NotificationAction.Open), panel.Press(PanelKey.Enter));
        panel.Press(PanelKey.Tab);
        Assert.Equal((1L, NotificationAction.ShowInFolder), panel.Press(PanelKey.Space));
    }

    [Fact]
    public void Tab_pula_o_que_esta_em_andamento_e_volta_ao_inicio()
    {
        var panel = new FilesPanelState(History(HistoryState.Failed, HistoryState.Running));

        Assert.Equal((1L, NotificationAction.Retry), panel.Press(PanelKey.Enter));
        panel.Press(PanelKey.Tab);
        Assert.Equal((1L, NotificationAction.Retry), panel.Press(PanelKey.Enter));
    }

    [Fact]
    public void Foco_rola_a_lista_ate_a_linha_aberta_inteira()
    {
        var panel = new FilesPanelState(History(Done(8)));

        for (var i = 0; i < 14; i++)
        {
            panel.Press(PanelKey.Tab);
        }

        Assert.Equal(7, panel.OpenRow);
        Assert.Equal(panel.MaxScroll, panel.Scroll);

        for (var i = 0; i < 14; i++)
        {
            panel.Press(PanelKey.ShiftTab);
        }

        Assert.Equal(0, panel.OpenRow);
        Assert.Equal(0, panel.Scroll);
    }

    [Fact]
    public void Clique_no_botao_da_linha_aberta_executa_considerando_a_rolagem()
    {
        var panel = new FilesPanelState(History(Done(8)));
        for (var i = 0; i < 14; i++)
        {
            panel.Press(PanelKey.Tab);
        }

        var open = FilesPanelState.Buttons(HistoryState.Done)[0].Bounds;

        // Com a lista no fim, a linha aberta é a do item 1 (o mais antigo fica embaixo).
        var action = panel.Click(open.Left + 1, Y(panel, 7, open.Top + 1));

        Assert.Equal((1L, NotificationAction.Open), action);
    }

    [Fact]
    public void Clique_numa_linha_fechada_a_abre_sem_executar()
    {
        var panel = new FilesPanelState(History(Done(3)));
        var height = panel.Height;

        Assert.Null(panel.Click(60, Y(panel, 2, 10)));

        Assert.Equal(2, panel.OpenRow);
        Assert.Equal((2, NotificationAction.Open), panel.Focused);
        Assert.Equal(height, panel.Height);
    }

    [Fact]
    public void Clique_em_linha_em_andamento_nao_muda_o_foco()
    {
        // A mais recente fica em cima: a linha 0 é a que está em andamento.
        var panel = new FilesPanelState(History(HistoryState.Done, HistoryState.Running));

        Assert.Null(panel.Click(60, Y(panel, 0, 10)));

        Assert.Equal(1, panel.OpenRow);
    }

    [Fact]
    public void Clique_fora_de_botao_e_no_cabecalho_nao_faz_nada()
    {
        var panel = new FilesPanelState(History(HistoryState.Done));

        Assert.Null(panel.Click(1, 1));
        Assert.Null(panel.Click(1, FilesPanelState.HeaderHeight + 1));
        Assert.Equal(0, panel.OpenRow);
    }

    [Fact]
    public void Item_em_andamento_nao_tem_botoes()
    {
        Assert.Empty(FilesPanelState.Buttons(HistoryState.Running));
    }

    [Fact]
    public void Lista_que_diminui_nao_deixa_foco_nem_rolagem_para_fora()
    {
        var history = History(Done(8));
        var panel = new FilesPanelState(history);
        panel.Wheel(-120 * 50);
        for (var i = 0; i < 15; i++)
        {
            panel.Press(PanelKey.Tab);
        }

        for (long id = 1; id <= 7; id++)
        {
            history.Remove(id);
        }

        panel.Refresh();

        Assert.Equal(0, panel.Scroll);
        Assert.Equal(0, panel.OpenRow);
        Assert.Equal(8, panel.Press(PanelKey.Enter)!.Value.Id);
    }

    [Fact]
    public void Botoes_ficam_dentro_da_linha_aberta_com_alvo_de_44_px()
    {
        foreach (var state in new[] { HistoryState.Done, HistoryState.Failed })
        {
            foreach (var button in FilesPanelState.Buttons(state))
            {
                Assert.True(button.Bounds.Right <= FilesPanelState.Width && button.Bounds.Bottom <= FilesPanelState.OpenHeight);
                Assert.True(button.Bounds.Top >= FilesPanelState.RowHeight);
                Assert.Equal(44, button.Bounds.Bottom - button.Bounds.Top);
            }
        }
    }

    [Fact]
    public void Status_segue_a_prancha()
    {
        var history = History(HistoryState.Done, HistoryState.Running, HistoryState.Failed, HistoryState.Done);
        history.Items[3].Missing = true;

        Assert.Equal("Concluído · PNG", FilesPanelState.Status(history.Items[0]));
        Assert.Equal("Erro · motivo", FilesPanelState.Status(history.Items[1]));
        Assert.Equal("Convertendo…", FilesPanelState.Status(history.Items[2]));
        Assert.Equal("Arquivo não encontrado", FilesPanelState.Status(history.Items[3]));
    }
}
