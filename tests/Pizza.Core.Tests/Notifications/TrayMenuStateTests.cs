using Pizza.Core.Conversions;
using Pizza.Core.Notifications;

namespace Pizza.Core.Tests.Notifications;

/// <summary>ADR-020, prancha 4d-2: menu da bandeja em vidro, com uso completo pelo teclado (NFR-06).</summary>
public class TrayMenuStateTests
{
    private static readonly PanelRect Screen = new(0, 0, 1920, 1040);

    [Fact]
    public void Itens_na_ordem_do_adr_020_so_os_que_ja_funcionam()
    {
        // Configuração (Fase 8) e Play, Pause e Stop (v1.x) só aparecem quando funcionarem.
        var menu = new TrayMenuState();

        Assert.Equal(["Arquivos…", "Abrir pasta de logs", "Sair"], menu.Items.Select(item => item.Label));
        Assert.Equal([TrayCommand.Files, TrayCommand.OpenLogs, TrayCommand.Quit], menu.Items.Select(item => item.Command));
    }

    [Fact]
    public void Enter_executa_o_primeiro_item_e_esc_fecha()
    {
        var menu = new TrayMenuState();

        Assert.Equal(TrayCommand.Files, menu.Press(PanelKey.Enter));
        Assert.Equal(TrayCommand.Close, menu.Press(PanelKey.Escape));
    }

    [Fact]
    public void Setas_e_tab_percorrem_os_itens_dando_a_volta()
    {
        var menu = new TrayMenuState();

        Assert.Null(menu.Press(PanelKey.Down));
        Assert.Equal(1, menu.Focus);
        menu.Press(PanelKey.Tab);
        Assert.Equal(TrayCommand.Quit, menu.Press(PanelKey.Space));
        menu.Press(PanelKey.Down);
        Assert.Equal(0, menu.Focus);
        menu.Press(PanelKey.Up);
        Assert.Equal(2, menu.Focus);
        menu.Press(PanelKey.ShiftTab);
        Assert.Equal(1, menu.Focus);
    }

    [Fact]
    public void Mouse_sobre_um_item_move_o_foco_e_o_clique_executa()
    {
        var menu = new TrayMenuState();
        var quit = menu.Items[2].Bounds;

        Assert.True(menu.Hover(quit.Left + 5, quit.Top + 5));
        Assert.Equal(2, menu.Focus);
        Assert.False(menu.Hover(quit.Left + 6, quit.Top + 6));
        Assert.Equal(TrayCommand.Quit, menu.Click(quit.Right - 1, quit.Bottom - 1));
    }

    [Fact]
    public void Clique_no_titulo_ou_entre_itens_nao_faz_nada()
    {
        var menu = new TrayMenuState();

        Assert.Null(menu.Click(20, TrayMenuState.HeaderTop + 5));
        Assert.Null(menu.Click(20, menu.Items[1].Bounds.Bottom));
        Assert.False(menu.Hover(1, 1));
        Assert.Equal(0, menu.Focus);
    }

    [Fact]
    public void Itens_tem_alvo_de_44_px_dentro_do_menu_sem_se_sobrepor()
    {
        var menu = new TrayMenuState();

        foreach (var item in menu.Items)
        {
            Assert.Equal(44, item.Bounds.Bottom - item.Bounds.Top);
            Assert.True(item.Bounds is { Left: >= 0, Top: >= TrayMenuState.HeaderTop });
            Assert.True(item.Bounds.Right <= TrayMenuState.Width && item.Bounds.Bottom <= menu.Height);
        }

        for (var i = 1; i < menu.Items.Count; i++)
        {
            Assert.False(menu.Items[i].Bounds.Overlaps(menu.Items[i - 1].Bounds));
        }
    }

    [Theory]
    [InlineData(1900, 1030)]
    [InlineData(10, 1030)]
    [InlineData(1900, 5)]
    [InlineData(960, 520)]
    public void Abre_junto_ao_cursor_inteiro_na_area_de_trabalho(int x, int y)
    {
        var menu = new TrayMenuState();

        var (left, top) = TrayMenuState.Place(x, y, TrayMenuState.Width, menu.Height, Screen);

        Assert.InRange(left, Screen.Left, Screen.Right - TrayMenuState.Width);
        Assert.InRange(top, Screen.Top, Screen.Bottom - menu.Height);
    }

    [Fact]
    public void Perto_do_relogio_abre_acima_e_a_esquerda_do_cursor()
    {
        var menu = new TrayMenuState();

        var (left, top) = TrayMenuState.Place(1800, 1020, TrayMenuState.Width, menu.Height, Screen);

        Assert.Equal(1800 - TrayMenuState.Width, left);
        Assert.Equal(1020 - menu.Height, top);
    }
}

