using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Pizza.Core.Ipc;
using Pizza.Core.Output;

namespace Pizza.Integration.Tests;

/// <summary>
/// Aba Áudio de ponta a ponta (ADR-035): o Media Foundation converte dentro do Worker em AppContainer, com a política
/// de só binários da Microsoft ligada (SEC-26), lendo e gravando só pelos handles (SEC-15, SEC-22).
/// </summary>
[Collection(SandboxCollection.Name)]
public sealed class AudioConversionTests(SandboxFixture fixture) : IDisposable
{
    private readonly string _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pizza-audio-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
        File.Delete(_folder + ".journal");
    }

    [MediaFact]
    public void Wav_vira_mp3_m4a_e_flac_ao_lado_sem_tocar_no_original()
    {
        var source = Path.Combine(_folder, "gravacao.wav");
        File.WriteAllBytes(source, Wav(seconds: 2));
        var before = SHA256.HashData(File.ReadAllBytes(source));

        foreach (var (id, extension, options) in new[]
        {
            ("audio.mp3", "mp3", new Dictionary<string, string> { ["bitrate"] = "320", ["channels"] = "mono" }),
            ("audio.m4a", "m4a", new Dictionary<string, string> { ["bitrate"] = "lossless" }),
            ("audio.flac", "flac", new Dictionary<string, string> { ["depth"] = "24", ["rate"] = "48000" }),
        })
        {
            var result = Runner().Run(source, id, extension, options);

            Assert.Null(result.ErrorCode);
            Assert.Equal("gravacao." + extension, result.OutputName);
            Assert.True(new FileInfo(Path.Combine(_folder, result.OutputName!)).Length > 1000, id);
        }

        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(source)));
        Assert.Empty(Directory.GetFiles(_folder, ".~pizza-*", new EnumerationOptions { AttributesToSkip = 0 }));
    }

    [MediaFact]
    public void Volume_com_normalizar_le_duas_vezes_pelo_mesmo_handle_no_worker()
    {
        // Adendo do ADR-035: a medição e a gravação voltam ao começo da origem pelo handle (SEC-22), dentro do prazo.
        var source = Path.Combine(_folder, "aula.wav");
        File.WriteAllBytes(source, Wav(seconds: 2));
        var options = new Dictionary<string, string> { ["gain"] = "3", ["compress"] = "-20", ["ratio"] = "4", ["normalize"] = "-14", ["limit"] = "-1" };

        var result = Runner().Run(source, "audio.mp3", "mp3", options);

        Assert.Null(result.ErrorCode);
        Assert.True(new FileInfo(Path.Combine(_folder, result.OutputName!)).Length > 1000);
    }

    [MediaFact]
    public void Arquivo_que_nao_e_audio_e_recusado_sem_gerar_saida()
    {
        // O formato é conferido pelo conteúdo: um texto com extensão .mp3 não chega ao decodificador.
        var source = Path.Combine(_folder, "falso.mp3");
        File.WriteAllText(source, "isto não é um MP3, só tem a extensão");

        var result = Runner().Run(source, "audio.wav", "wav");

        Assert.Equal("UNSUPPORTED", result.ErrorCode);
        Assert.Single(Directory.GetFiles(_folder, "*", new EnumerationOptions { AttributesToSkip = 0 }));
    }

    private ConversionRunner Runner() =>
        new(fixture.WorkerPath, SandboxFixture.Profile, new TempJournal(_folder + ".journal"), TimeSpan.FromSeconds(60));

    private static byte[] Wav(int seconds)
    {
        const int rate = 44100, channels = 2;
        var frames = rate * seconds;
        var data = frames * channels * 2;
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);
        writer.Write("RIFF"u8);
        writer.Write(36 + data);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(rate);
        writer.Write(rate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(data);
        for (var i = 0; i < frames; i++)
        {
            var value = (short)(Math.Sin(2 * Math.PI * 440 * i / rate) * 12000);
            writer.Write(value);
            writer.Write(value);
        }

        writer.Flush();
        return memory.ToArray();
    }
}

/// <summary>Teste que precisa do Media Foundation; num Windows sem ele aparece como ignorado, nunca como aprovado.</summary>
public sealed class MediaFactAttribute : FactAttribute
{
    public MediaFactAttribute()
    {
        if (!NativeLibrary.TryLoad("mfreadwrite.dll", out var library))
        {
            Skip = "Media Foundation ausente neste Windows (ADR-035).";
            return;
        }

        NativeLibrary.Free(library);
    }
}
