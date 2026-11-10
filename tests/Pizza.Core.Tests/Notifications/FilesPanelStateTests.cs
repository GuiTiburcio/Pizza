using Pizza.Core.Conversions;
using Pizza.Core.Notifications;

namespace Pizza.Core.Tests.Notifications;

/// <summary>FR-44 e NFR-06: lista rolável e uso completo pelo teclado.</summary>
public class FilesPanelStateTests
{
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

    [Fact]
    public void Lista_vazia_tem_area_para_o_aviso()
    {
        var panel = new FilesPanelState(History());

        Assert.Equal(FilesPanelState.HeaderHeight + FilesPanelState.EmptyHeight, panel.Height);
        Assert.Null(panel.Press(PanelKey.Enter));
    }

    [Fact]
    public void Altura_cresce_com_a_lista_ate_o_limite()
    {
        var one = new FilesPanelState(History(Done(1)));
        var many = new FilesPanelState(History(Done(30)));

        Assert.Equal(FilesPanelState.HeaderHeight + FilesPanelState.RowHeight, one.Height);
        Assert.Equal(FilesPanelState.HeaderHeight + (4 * FilesPanelState.RowHeight), many.Height);
    }

    [Fact]
    public void Roda_do_mouse_rola_sem_passar_dos_limites()
    {
        var panel = new FilesPanelState(History(Done(6)));

        panel.Wheel(120);
        Assert.Equal(0, panel.Scroll);

        panel.Wheel(-120);
        Assert.True(panel.Scroll > 0);

        for (var i = 0; i < 50; i++)
        {
            panel.Wheel(-120);
        }

        Assert.Equal(2 * FilesPanelState.RowHeight, panel.Scroll);
    }

    [Fact]
    public void Lista_que_cabe_nao_rola()
    {
        var panel = new FilesPanelState(History(Done(2)));

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
    public void Foco_rola_a_lista_ate_o_botao()
    {
        var panel = new FilesPanelState(History(Done(6)));

        for (var i = 0; i < 10; i++)
        {
            panel.Press(PanelKey.Tab);
        }

        Assert.Equal(2 * FilesPanelState.RowHeight, panel.Scroll);

        panel.Press(PanelKey.ShiftTab);
        for (var i = 0; i < 9; i++)
        {
            panel.Press(PanelKey.ShiftTab);
        }

        Assert.Equal(0, panel.Scroll);
    }

    [Fact]
    public void Clique_considera_a_rolagem()
    {
        var panel = new FilesPanelState(History(Done(6)));
        panel.Wheel(-120 * 50);
        var open = FilesPanelState.Buttons(HistoryState.Done)[0].Bounds;

        // Com a lista no fim, a primeira linha visível é a do item 4.
        var action = panel.Click(open.Left + 1, FilesPanelState.HeaderHeight + open.Top + 1);

        Assert.Equal((4L, NotificationAction.Open), action);
    }

    [Fact]
    public void Clique_fora_de_botao_e_no_cabecalho_nao_faz_nada()
    {
        var panel = new FilesPanelState(History(HistoryState.Done));

        Assert.Null(panel.Click(1, 1));
        Assert.Null(panel.Click(1, FilesPanelState.HeaderHeight + 1));
    }

    [Fact]
    public void Item_em_andamento_nao_tem_botoes()
    {
        Assert.Empty(FilesPanelState.Buttons(HistoryState.Running));
    }

    [Fact]
    public void Lista_que_diminui_nao_deixa_foco_nem_rolagem_para_fora()
    {
        var history = History(Done(6));
        var panel = new FilesPanelState(history);
        panel.Wheel(-120 * 50);
        for (var i = 0; i < 11; i++)
        {
            panel.Press(PanelKey.Tab);
        }

        for (long id = 1; id <= 5; id++)
        {
            history.Remove(id);
        }

        panel.Refresh();

        Assert.Equal(0, panel.Scroll);
        Assert.Equal(6, panel.Press(PanelKey.Enter)!.Value.Id);
    }

    [Fact]
    public void Botoes_ficam_dentro_da_linha()
    {
        foreach (var state in new[] { HistoryState.Done, HistoryState.Failed })
        {
            foreach (var button in FilesPanelState.Buttons(state))
            {
                Assert.True(button.Bounds.Right <= FilesPanelState.Width && button.Bounds.Bottom <= FilesPanelState.RowHeight);
            }
        }
    }
}
