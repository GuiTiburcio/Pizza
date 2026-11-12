using Pizza.Core.Conversions;
using Pizza.Core.Gesture;
using Pizza.Core.Ipc;

namespace Pizza.Core.Tests.Conversions;

public class AudioCatalogTests
{
    [Theory]
    [InlineData("tom.wav", "audio.wav")]
    [InlineData("tom.mp3", "audio.mp3")]
    [InlineData("tom.m4a", "audio.m4a")]
    [InlineData("tom.flac", "audio.flac")]
    public void Audio_abre_sua_aba_sem_o_proprio_formato(string file, string own)
    {
        var page = RadialMenu.Pages(file)[RadialMenu.FirstPage(file)];
        Assert.Equal(3, page.Tab);
        var slices = RadialMenu.Slices(page.Tab, file);
        Assert.Equal(3, slices.Count);
        Assert.DoesNotContain(slices, c => c.Id == own);
    }

    [Fact]
    public void Adts_aceita_os_quatro_destinos_e_video_nao_entra_no_catalogo_audio()
    {
        Assert.Equal(4, RadialMenu.Slices(3, "tom.aac").Count);
        Assert.False(RadialMenu.CanConvert("video.mp4"));
    }

    [Theory]
    [InlineData("audio.mp3", "192")]
    [InlineData("audio.m4a", "160")]
    public void Padrao_preserva_canais_e_taxa_sem_efeitos(string id, string bitrate)
    {
        var info = ConverterCatalog.Find(id)!;
        var options = ConverterCatalog.ResolveOptions(info, new Dictionary<string, string>())!;
        Assert.Equal(bitrate, options["bitrate"]);
        Assert.Equal("original", options["channels"]);
        Assert.Equal("original", options["sampleRate"]);
        Assert.Null(ConverterCatalog.ResolveOptions(info, new Dictionary<string, string> { ["gain"] = "24" }));
        Assert.Null(ConverterCatalog.ResolveOptions(info, new Dictionary<string, string> { ["metadata"] = "keep" }));
    }

    [Fact]
    public void Windows_sem_midia_tem_mensagem_especifica() =>
        Assert.Contains("Media Feature Pack", ConversionMessages.For("MEDIA_UNAVAILABLE"));

    [Fact]
    public void Ausencia_de_midia_atravessa_o_protocolo_sem_virar_queda_do_worker()
    {
        var message = new IpcMessage(IpcType.Error, "0123456789ab") { Code = "MEDIA_UNAVAILABLE" };
        Assert.Equal("MEDIA_UNAVAILABLE", IpcCodec.Parse(IpcCodec.Serialize(message)).Code);
    }

    [Theory]
    [InlineData("audio.mp3", "320", "192", "128")]
    [InlineData("audio.m4a", "256", "160", "96")]
    public void Faixa_mostra_tres_niveis_e_painel_mantem_o_catalogo_completo(string id, string high, string balanced, string compact)
    {
        var converter = ConverterCatalog.Find(id)!;
        var state = new QuickOptionsState();
        state.PointAt(converter);
        Assert.Equal(new[] { high, balanced, compact }, state.Primary!.Choices.Select(c => c.Value));
        Assert.Equal(balanced, state.Options()!["bitrate"]);
        Assert.Equal(8, converter.Options[0].Choices.Count);
        state.Step(1);
        Assert.Equal(compact, state.Options()!["bitrate"]);
    }
}
