using System.Buffers.Binary;
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
public sealed class AudioConversionTests(SandboxFixture fixture, Xunit.Abstractions.ITestOutputHelper output) : IDisposable
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
            ("audio.mp3", "mp3", new Dictionary<string, string> { ["bitrate"] = "96", ["channels"] = "mono" }),
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
    public void M4a_sem_perda_volta_para_wav_no_worker_sem_alterar_as_amostras()
    {
        // TST-02/TST-10 e SEC-26: a releitura também precisa funcionar dentro do Worker com a política de assinatura.
        var source = Path.Combine(_folder, "original.wav");
        var original = Wav(seconds: 1);
        File.WriteAllBytes(source, original);
        var encoded = Runner().Run(source, "audio.m4a", "m4a", new Dictionary<string, string> { ["bitrate"] = "lossless" });
        Assert.Null(encoded.ErrorCode);

        var encodedPath = Path.Combine(_folder, encoded.OutputName!);
        var encodedBefore = File.ReadAllBytes(encodedPath);
        var decoded = Runner().Run(encodedPath, "audio.wav", "wav");

        Assert.Null(decoded.ErrorCode);
        Assert.Equal(original, File.ReadAllBytes(source));
        Assert.Equal(encodedBefore, File.ReadAllBytes(encodedPath));
        Assert.Equal(Pcm(original), Pcm(File.ReadAllBytes(Path.Combine(_folder, decoded.OutputName!))));
        Assert.Empty(Directory.GetFiles(_folder, ".~pizza-*", new EnumerationOptions { AttributesToSkip = 0 }));
    }

    [MediaFact]
    public void Alac_de_uma_hora_respeita_prazo_e_preserva_PCM()
    {
        // ADR-035 / SEC-43: uma hora real de PCM sintético, em fluxo; não é um cabeçalho com duração falsa.
        var source = Path.Combine(_folder, "uma-hora.wav");
        var second = Wav(1);
        var header = second.AsSpan(0, 44).ToArray();
        var dataLength = checked((second.Length - 44) * 3600);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), 36 + dataLength);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(40, 4), dataLength);
        using (var file = File.Create(source))
        {
            file.Write(header);
            for (var index = 0; index < 3600; index++) file.Write(second.AsSpan(44));
        }

        var before = FileHash(source);
        var runner = new ConversionRunner(fixture.WorkerPath, SandboxFixture.Profile, new TempJournal(_folder + ".journal"), TimeSpan.FromMinutes(2));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var encoded = runner.Run(source, "audio.m4a", "m4a", new Dictionary<string, string> { ["bitrate"] = "lossless" });
        output.WriteLine($"ALAC de uma hora: {clock.Elapsed.TotalSeconds:F2} s.");
        Assert.Null(encoded.ErrorCode);
        var decoded = runner.Run(Path.Combine(_folder, encoded.OutputName!), "audio.wav", "wav");
        Assert.Null(decoded.ErrorCode);
        Assert.Equal(PcmHash(source), PcmHash(Path.Combine(_folder, decoded.OutputName!)));
        Assert.Equal(before, FileHash(source));
        Assert.Empty(Directory.GetFiles(_folder, ".~pizza-*", new EnumerationOptions { AttributesToSkip = 0 }));
    }

    [MediaFact]
    public void Taxa_menor_so_converte_depois_de_confirmada()
    {
        // TST-02/TST-10: Worker real, sem saída final ou temporário enquanto a oferta aguarda o usuário.
        var source = Path.Combine(_folder, "voz.wav");
        File.WriteAllBytes(source, Wav(seconds: 1));
        var before = SHA256.HashData(File.ReadAllBytes(source));
        var options = new Dictionary<string, string> { ["bitrate"] = "320", ["channels"] = "mono" };

        var offer = Runner().Run(source, "audio.mp3", "mp3", options);

        Assert.Equal(Pizza.Core.Conversions.BitrateOffer.Required, offer.ErrorCode);
        Assert.Null(offer.OutputName);
        Assert.NotNull(offer.SuggestedBitrate);
        Assert.Single(Directory.GetFiles(_folder, "*", new EnumerationOptions { AttributesToSkip = 0 }));
        var accepted = Pizza.Core.Conversions.BitrateOffer.Accept("audio.mp3", options, offer.SuggestedBitrate.Value);
        Assert.NotNull(accepted);

        var converted = Runner().Run(source, "audio.mp3", "mp3", accepted);

        Assert.Null(converted.ErrorCode);
        Assert.Equal("voz.mp3", converted.OutputName);
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
    public void Probe_informa_a_duracao_e_o_mesmo_worker_converte_depois()
    {
        // Adendo do ADR-035: o painel pede "probe" ao Worker do arquivo; a conversão reaproveita esse Worker (ADR-019).
        var source = Path.Combine(_folder, "aula.wav");
        File.WriteAllBytes(source, Wav(seconds: 2));

        var probed = Runner().Probe(source, "audio.mp3", TimeSpan.FromSeconds(10));
        Assert.NotNull(probed);
        Assert.Equal(44100, probed.Info!.SampleRate);
        Assert.Equal(2, probed.Info.Channels);
        Assert.InRange(probed.Info.DurationMs, 1950, 2050);

        var result = Runner().Run(source, "audio.mp3", "mp3", reuse: probed);

        Assert.Null(result.ErrorCode);
        Assert.True(new FileInfo(Path.Combine(_folder, result.OutputName!)).Length > 1000);
    }

    [MediaFact]
    public void Probe_de_arquivo_que_nao_e_audio_nao_informa_nada_e_a_conversao_recusa()
    {
        var source = Path.Combine(_folder, "falso.mp3");
        File.WriteAllText(source, "isto não é um MP3");

        var probed = Runner().Probe(source, "audio.wav", TimeSpan.FromSeconds(10));
        Assert.NotNull(probed);
        Assert.Null(probed.Info);

        Assert.Equal("UNSUPPORTED", Runner().Run(source, "audio.wav", "wav", reuse: probed).ErrorCode);
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

    private static byte[] FileHash(string path)
    {
        using var file = File.OpenRead(path);
        return SHA256.HashData(file);
    }

    private static (long Bytes, string Hash) PcmHash(string path)
    {
        using var file = File.OpenRead(path);
        using var reader = new BinaryReader(file);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)));
        file.Position = 12;
        while (file.Position + 8 <= file.Length)
        {
            var name = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
            var size = reader.ReadUInt32();
            Assert.InRange((long)size, 0, file.Length - file.Position);
            if (name != "data")
            {
                file.Position += size + (size & 1);
                continue;
            }

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[65536];
            long remaining = size;
            while (remaining > 0)
            {
                var count = file.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                Assert.True(count > 0);
                hash.AppendData(buffer, 0, count);
                remaining -= count;
            }

            return (size, Convert.ToHexString(hash.GetHashAndReset()));
        }

        throw new InvalidDataException("WAV de teste sem PCM.");
    }

    private static byte[] Pcm(byte[] wav)
    {
        Assert.True(wav.AsSpan(0, 4).SequenceEqual("RIFF"u8));
        for (var offset = 12; offset + 8 <= wav.Length;)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(wav.AsSpan(offset + 4, 4)));
            Assert.InRange(length, 0, wav.Length - offset - 8);
            if (wav.AsSpan(offset, 4).SequenceEqual("data"u8)) return wav.AsSpan(offset + 8, length).ToArray();
            offset = checked(offset + 8 + length + (length & 1));
        }
        throw new InvalidDataException("WAV de teste sem chunk data.");
    }

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
