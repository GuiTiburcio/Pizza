using System.Security.Cryptography;
using Pizza.Core.Ipc;
using Pizza.Core.Output;

namespace Pizza.Integration.Tests;

/// <summary>TST-02/TST-10, SEC-20/31: áudio pelo Worker real, somente handles e saída segura.</summary>
[Collection(SandboxCollection.Name)]
public sealed class AudioConversionTests(SandboxFixture fixture) : IDisposable
{
    private readonly string _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pizza-audio-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
        File.Delete(_folder + ".journal");
    }

    [Theory]
    [InlineData("audio.mp3", "mp3")]
    [InlineData("audio.m4a", "m4a")]
    [InlineData("audio.flac", "flac")]
    [InlineData("audio.wav", "wav")]
    public void Worker_converte_audio_e_le_a_saida_sem_alterar_os_originais(string id, string extension)
    {
        var source = Write("tom.wav", AudioSamples.Wave());
        var originalHash = Hash(source);
        var result = Runner().Run(source, id, extension, OutputOptions(id));

        Assert.True(result.ErrorCode is null, $"{id}: {result.ErrorCode}; o runner deve ter Media Foundation para validar os codecs.");
        Assert.NotNull(result.OutputName);
        var encoded = Path.Combine(_folder, result.OutputName);
        Assert.True(File.Exists(encoded));
        Assert.Equal(originalHash, Hash(source));
        var encodedHash = Hash(encoded);

        var back = Runner().Run(encoded, "audio.wav", "wav", OutputOptions("audio.wav"));
        Assert.Null(back.ErrorCode);
        Assert.NotNull(back.OutputName);
        var decoded = AudioSamples.ReadWave(File.ReadAllBytes(Path.Combine(_folder, back.OutputName)));
        Assert.Equal((2, AudioSamples.Rate, 16), (decoded.Channels, decoded.Rate, decoded.Bits));
        Assert.InRange(decoded.Data.Length / (decoded.Rate * decoded.Channels * (decoded.Bits / 8.0)), 0.15, 0.36);
        Assert.Equal(encodedHash, Hash(encoded));
        Assert.Equal(originalHash, Hash(source));
        AssertNoTemporaries();
    }

    [Fact]
    public void Arquivo_disfarcado_de_mp3_nao_produz_saida_e_preserva_original()
    {
        var source = Write("falso.mp3", "Isto nao e um arquivo de audio."u8.ToArray());
        var before = Hash(source);

        var result = Runner().Run(source, "audio.wav", "wav", OutputOptions("audio.wav"));

        Assert.Equal("UNSUPPORTED", result.ErrorCode);
        Assert.Equal(before, Hash(source));
        Assert.Single(Directory.GetFiles(_folder, "*", new EnumerationOptions { AttributesToSkip = 0 }));
        AssertNoTemporaries();
    }

    [Fact]
    public void Cancelar_worker_pausado_de_audio_remove_temporario_sem_tocar_na_origem()
    {
        // Pausa antes de executar o codec: cancelamento não disputa com uma conversão muito curta.
        var source = Write("cancelar.wav", AudioSamples.Wave());
        var before = Hash(source);
        var control = new ConversionControl { Paused = true };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        var result = Runner().Run(source, "audio.mp3", "mp3", OutputOptions("audio.mp3"), cancellation.Token, control);

        Assert.Equal(ConversionRunner.Cancelled, result.ErrorCode);
        Assert.Equal(before, Hash(source));
        Assert.Single(Directory.GetFiles(_folder, "*", new EnumerationOptions { AttributesToSkip = 0 }));
        AssertNoTemporaries();
    }

    private static IReadOnlyDictionary<string, string> OutputOptions(string id)
    {
        var options = new Dictionary<string, string> { ["channels"] = "original", ["sampleRate"] = "44100" };
        if (id is "audio.wav" or "audio.flac") options["depth"] = "16";
        return options;
    }

    private ConversionRunner Runner() => new(fixture.WorkerPath, SandboxFixture.Profile, new TempJournal(_folder + ".journal"), TimeSpan.FromSeconds(60));

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] Hash(string path) => SHA256.HashData(File.ReadAllBytes(path));
    private void AssertNoTemporaries() => Assert.Empty(Directory.GetFiles(_folder, ".~pizza-*", new EnumerationOptions { AttributesToSkip = 0 }));
}
