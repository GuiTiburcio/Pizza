using Pizza.Core.Conversions;
using Pizza.Core.Notifications;

namespace Pizza.Core.Tests.Conversions;

public sealed class BitrateOfferTests
{
    [Theory]
    [InlineData("audio.mp3", "320", 192, true)]
    [InlineData("audio.m4a", "320", 256, true)]
    [InlineData("audio.mp3", "320", 320, false)]
    [InlineData("audio.mp3", "128", 192, false)]
    [InlineData("audio.mp3", "320", 319, false)]
    [InlineData("audio.mp3", "319", 192, false)]
    [InlineData("audio.m4a", "lossless", 192, false)]
    [InlineData("audio.wav", "320", 192, false)]
    [InlineData("image.png", "320", 192, false)]
    public void Oferta_so_aceita_reducao_numerica_do_catalogo(string id, string requested, int suggested, bool valid)
    {
        Assert.Equal(valid, BitrateOffer.IsValid(id, new Dictionary<string, string> { ["bitrate"] = requested }, suggested));
    }

    [Fact]
    public void Oferta_espera_confirmacao_e_nao_muda_as_opcoes_originais()
    {
        var options = new Dictionary<string, string> { ["bitrate"] = "320", ["channels"] = "mono", ["rate"] = "48000" };
        var history = new ConversionHistory();
        history.Add(1, "gravacao.wav", ConverterCatalog.Find("audio.mp3")!, options);
        Assert.True(history.OfferBitrate(1, 192));
        var entry = history.Find(1)!;
        Assert.Equal(HistoryState.AwaitingConfirmation, entry.State);
        Assert.Null(entry.OutputPath);
        Assert.Equal("320", entry.Options["bitrate"]);
        Assert.Contains("320 kbps", entry.Message);
        Assert.Contains("192 kbps", entry.Message);
        options["bitrate"] = "128";

        var accepted = history.AcceptBitrate(1);
        Assert.NotNull(accepted);
        Assert.Equal("192", accepted["bitrate"]);
        Assert.Equal("mono", accepted["channels"]);
        Assert.Equal("48000", accepted["rate"]);
        Assert.Null(history.AcceptBitrate(1));
        Assert.Equal("320", entry.Options["bitrate"]);
    }

    [Fact]
    public void Fechar_preserva_oferta_e_uma_nova_reducao_exige_novo_aceite()
    {
        var history = new ConversionHistory();
        var converter = ConverterCatalog.Find("audio.mp3")!;
        history.Add(1, "a.wav", converter, new Dictionary<string, string> { ["bitrate"] = "320" });
        Assert.True(history.OfferBitrate(1, 192));
        var stack = new NotificationStack();
        stack.Set(1, NotificationKind.Confirmation, "Confirmar qualidade", history.Find(1)!.Message!);
        stack.Dismiss(1);
        Assert.Equal(HistoryState.AwaitingConfirmation, history.Find(1)!.State);
        var files = new FilesPanelState(history);
        Assert.Equal((1L, NotificationAction.AcceptBitrate), files.Press(PanelKey.Enter));
        Assert.Null(files.Press(PanelKey.Tab));
        Assert.Equal((1L, NotificationAction.Cancel), files.Press(PanelKey.Enter));

        var accepted = history.AcceptBitrate(1)!;
        history.Remove(1);
        history.Add(2, "a.wav", converter, accepted);
        Assert.True(history.OfferBitrate(2, 128));
        Assert.Equal("192", history.Find(2)!.Options["bitrate"]);
        Assert.Null(history.Find(2)!.OutputPath);
        Assert.Null(history.AcceptBitrate(1));
        Assert.Equal("128", history.AcceptBitrate(2)!["bitrate"]);
    }

    [Fact]
    public void Cancelar_nao_confirma_e_oferta_nao_expira_sozinha()
    {
        var history = new ConversionHistory();
        history.Add(1, "a.wav", ConverterCatalog.Find("audio.mp3")!, new Dictionary<string, string> { ["bitrate"] = "320" });
        Assert.True(history.OfferBitrate(1, 192));
        var stack = new NotificationStack();
        stack.Set(1, NotificationKind.Confirmation, "Confirmar qualidade", history.Find(1)!.Message!);
        stack.Advance(86_400_000);
        Assert.Single(stack.Visible);
        Assert.False(stack.NeedsClock);
        history.Remove(1);
        Assert.Null(history.AcceptBitrate(1));
        Assert.Contains(NotificationAction.Cancel, NotificationCardLayout.Buttons(NotificationKind.Confirmation).Select(button => button.Action));
        Assert.DoesNotContain(NotificationAction.Retry, FilesPanelState.Buttons(HistoryState.AwaitingConfirmation).Select(button => button.Action));
    }
}
