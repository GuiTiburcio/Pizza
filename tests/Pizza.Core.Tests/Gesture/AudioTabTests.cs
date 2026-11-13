using Pizza.Core.Conversions;
using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

/// <summary>ADR-035: a aba Áudio converte para MP3, M4A, FLAC e WAV; a fatia do próprio formato não aparece.</summary>
public class AudioTabTests
{
    [Theory]
    [InlineData("gravacao.wav", new[] { "MP3", "M4A", "FLAC" })]
    [InlineData("musica.mp3", new[] { "M4A", "FLAC", "WAV" })]
    [InlineData("podcast.m4a", new[] { "MP3", "FLAC", "WAV" })]
    [InlineData("radio.aac", new[] { "MP3", "M4A", "FLAC", "WAV" })]
    [InlineData("disco.flac", new[] { "MP3", "M4A", "WAV" })]
    public void Arquivo_de_audio_abre_na_aba_Audio_sem_a_fatia_do_proprio_formato(string file, string[] labels)
    {
        var first = RadialMenu.Pages(file)[RadialMenu.FirstPage(file)];

        Assert.Equal(ConverterCatalog.AudioTab, first.Tab);
        Assert.Equal(labels, RadialMenu.Slices(ConverterCatalog.AudioTab, file).Select(slice => slice.Label));
    }

    [Theory]
    [InlineData("audio.mp3", "bitrate", "96", true)]
    [InlineData("audio.mp3", "bitrate", "320", true)]
    [InlineData("audio.mp3", "bitrate", "200", false)]
    [InlineData("audio.mp3", "bitrate", "64", false)]
    [InlineData("audio.mp3", "bitrate", "lossless", false)]
    [InlineData("audio.m4a", "bitrate", "64", true)]
    [InlineData("audio.m4a", "bitrate", "224", false)]
    [InlineData("audio.m4a", "bitrate", "lossless", true)]
    [InlineData("audio.flac", "depth", "24", true)]
    [InlineData("audio.flac", "depth", "32", false)]
    [InlineData("audio.wav", "channels", "stereo", true)]
    [InlineData("audio.wav", "rate", "48000", true)]
    [InlineData("audio.wav", "rate", "96000", false)]
    public void Esquema_so_aceita_os_valores_que_o_Windows_codifica(string id, string key, string value, bool accepted)
    {
        // SEC-13: o Worker valida com o mesmo catálogo; valores fora da lista do codificador são recusados.
        var converter = ConverterCatalog.Find(id)!;

        var resolved = ConverterCatalog.ResolveOptions(converter, new Dictionary<string, string> { [key] = value });

        Assert.Equal(accepted, resolved is not null);
    }

    [Fact]
    public void Profundidade_so_existe_nos_formatos_sem_perda()
    {
        Assert.Null(ConverterCatalog.ResolveOptions(ConverterCatalog.Find("audio.mp3")!, new Dictionary<string, string> { ["depth"] = "16" }));
        Assert.Null(ConverterCatalog.ResolveOptions(ConverterCatalog.Find("audio.flac")!, new Dictionary<string, string> { ["bitrate"] = "192" }));
    }

    [Theory]
    [InlineData("audio.mp3", "bitrate", "192")]
    [InlineData("audio.m4a", "bitrate", "160")]
    [InlineData("audio.flac", "depth", "original")]
    [InlineData("audio.wav", "depth", "original")]
    public void Padroes_seguem_as_pranchas(string id, string key, string expected)
    {
        var options = ConverterCatalog.ResolveOptions(ConverterCatalog.Find(id)!, new Dictionary<string, string>())!;

        Assert.Equal(expected, options[key]);
        Assert.Equal("original", options["channels"]);
        Assert.Equal("original", options["rate"]);
    }
}
