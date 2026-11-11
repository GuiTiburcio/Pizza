using Pizza.Core.Notifications;

namespace Pizza.Core.Tests.Notifications;

/// <summary>Botões do WF-4.</summary>
public class NotificationCardLayoutTests
{
    [Theory]
    [InlineData(NotificationKind.Progress, new[] { NotificationAction.Resume, NotificationAction.Pause, NotificationAction.Cancel })]
    [InlineData(NotificationKind.Success, new[] { NotificationAction.Open, NotificationAction.ShowInFolder, NotificationAction.Close })]
    [InlineData(NotificationKind.Error, new[] { NotificationAction.Retry, NotificationAction.Details, NotificationAction.Close })]
    public void Cada_tipo_tem_os_botoes_do_wireframe(NotificationKind kind, NotificationAction[] expected)
    {
        Assert.Equal(expected, NotificationCardLayout.Buttons(kind).Select(button => button.Action));
    }

    [Theory]
    [InlineData(NotificationKind.Progress)]
    [InlineData(NotificationKind.Success)]
    [InlineData(NotificationKind.Error)]
    public void Botoes_ficam_dentro_do_cartao_e_nao_se_sobrepoem(NotificationKind kind)
    {
        var buttons = NotificationCardLayout.Buttons(kind);
        foreach (var button in buttons)
        {
            Assert.True(button.Bounds is { Left: >= 0, Top: >= 0 });
            Assert.True(button.Bounds.Right <= NotificationCardLayout.Width && button.Bounds.Bottom <= NotificationCardLayout.Height);
        }

        for (var i = 0; i < buttons.Count; i++)
        {
            for (var j = i + 1; j < buttons.Count; j++)
            {
                Assert.False(buttons[i].Bounds.Overlaps(buttons[j].Bounds));
            }
        }
    }

    [Fact]
    public void Continuar_pausar_e_parar_tem_alvo_de_44_px_ao_lado_do_titulo()
    {
        // ADR-029 / NFR-06: o desenho tem 32 px, mas o alvo continua com 44 px.
        foreach (var button in NotificationCardLayout.Buttons(NotificationKind.Progress))
        {
            Assert.True(button.Bounds.Right - button.Bounds.Left >= 44 && button.Bounds.Bottom - button.Bounds.Top >= 44);
            Assert.True(button.Bounds.Bottom <= NotificationCardLayout.ProgressTextTop);
        }
    }

    [Fact]
    public void Ponto_encontra_o_botao()
    {
        var open = NotificationCardLayout.Buttons(NotificationKind.Success)[0];

        Assert.Equal(NotificationAction.Open, NotificationCardLayout.ButtonAt(NotificationKind.Success, open.Bounds.Left + 1, open.Bounds.Top + 1)!.Value.Action);
        Assert.Null(NotificationCardLayout.ButtonAt(NotificationKind.Success, 1, 1));
    }
}
