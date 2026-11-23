using System.Buffers.Binary;

namespace Pizza.Converters.Tests;

public partial class AudioConverterTests
{
    [MediaFact]
    public void Alac_leitor_respeita_pacote_parcial_explicito()
    {
        // Corrigir só a duração não bastou (CI 252). Isola agora o suporte do leitor ao quadro parcial do ALAC.
        // Fixture de um pacote, sem compressão: cada amostra real é mantida, inclusive o silêncio final.
        var source = PatternWave(16, 48000, 4095, 17);
        var (error, encoded) = Run("audio.m4a", source, ("bitrate", "lossless"));
        Assert.Null(error);
        encoded = ReplaceSingleAlacPacket(encoded, Pcm(source));
        M4aTimeline(encoded, (4095, 48000));
        var (decodeError, decoded) = Run("audio.wav", encoded);
        Assert.Null(decodeError);
        var expected = Pcm(source);
        var actual = Pcm(decoded);
        Assert.True(expected.SequenceEqual(actual), $"Pacote parcial explícito, PCM origem={expected.Length}, saída={actual.Length}; {M4aTimeline(encoded)}");
    }

    [MediaTheory]
    [InlineData("audio.flac", 16)]
    [InlineData("audio.flac", 24)]
    [InlineData("audio.m4a", 16)]
    [InlineData("audio.m4a", 24)]
    [InlineData("audio.wav", 16)]
    [InlineData("audio.wav", 24)]
    public void Sem_perda_preserva_cada_amostra_e_a_ordem_dos_canais(string id, int bits) =>
        AssertLosslessRoundtrip(id, bits, 48000, 48000, 0);

    [MediaTheory]
    [InlineData(16, 48000, 8192, 0)]
    [InlineData(24, 48000, 8192, 0)]
    [InlineData(16, 44100, 8192, 0)]
    [InlineData(24, 44100, 8192, 0)]
    [InlineData(16, 48000, 4095, 0)]
    [InlineData(16, 48000, 4096, 0)]
    [InlineData(16, 48000, 4097, 0)]
    [InlineData(24, 44100, 4095, 0)]
    [InlineData(24, 44100, 4096, 0)]
    [InlineData(24, 44100, 4097, 0)]
    [InlineData(16, 48000, 48001, 17)]
    [InlineData(24, 48000, 48001, 17)]
    [InlineData(16, 44100, 44101, 19)]
    [InlineData(24, 44100, 44101, 19)]
    public void Alac_preserva_pacotes_parciais_e_silencio_real_no_final(int bits, int rate, int frames, int trailingSilentFrames) =>
        AssertLosslessRoundtrip("audio.m4a", bits, rate, frames, trailingSilentFrames);

    [MediaTheory]
    [InlineData(16, 1, 1)]
    [InlineData(24, 1, 4095)]
    [InlineData(16, 2, 1)]
    [InlineData(24, 2, 4097)]
    [InlineData(16, 6, 4097)]
    [InlineData(16, 6, 8192)]
    [InlineData(24, 6, 4095)]
    public void Alac_preserva_cauda_curta_mono_estereo_e_multicanal(int bits, int channels, int frames) =>
        AssertLosslessRoundtrip("audio.m4a", bits, 48000, frames, Math.Min(frames, 7), channels);

    [MediaTheory]
    [InlineData("audio.wav")]
    [InlineData("audio.flac")]
    [InlineData("audio.m4a")]
    public void Mono_de_24_bits_nao_conta_alinhamento_RIFF_como_PCM(string id) =>
        AssertLosslessRoundtrip(id, 24, 48000, 4095, 7, 1);

    private static void AssertLosslessRoundtrip(string id, int bits, int rate, int frames, int trailingSilentFrames, int channels = 2)
    {
        // TST-02/TST-10: tamanho/duração iguais não provam fidelidade. Os canais diferem e incluem valores extremos.
        var source = PatternWave(bits, rate, frames, trailingSilentFrames, channels);
        var before = source.ToArray();
        var options = id == "audio.m4a" ? new[] { ("bitrate", "lossless") } : Array.Empty<(string, string)>();
        var (error, encoded) = Run(id, source, options);
        Assert.Null(error);
        if (id == "audio.m4a") Assert.Contains($"mdhd: escala={rate}, duração={frames}", M4aTimeline(encoded));
        var (decodeError, decoded) = Run("audio.wav", encoded);
        Assert.Null(decodeError);
        Assert.Equal(source, before);
        Assert.Equal(rate, Inspect(decoded).SampleRate);
        Assert.Equal(channels, Inspect(decoded).Channels);
        Assert.Equal(bits, Inspect(decoded).Bits);
        var expected = Pcm(source);
        var actual = Pcm(decoded);
        if (!expected.SequenceEqual(actual))
        {
            var mismatch = 0;
            while (mismatch < Math.Min(expected.Length, actual.Length) && expected[mismatch] == actual[mismatch]) mismatch++;
            Assert.Fail($"PCM do fixture: origem={expected.Length} bytes, saída={actual.Length} bytes, primeira diferença={mismatch}; codificado={Inspect(encoded)}; decodificado={Inspect(decoded)}; leitura da origem={InputPcmTimeline(source)}; contêiner={M4aTimeline(encoded)}");
        }
    }

    private static byte[] PatternWave(int bits, int rate, int frames, int trailingSilentFrames, int channels = 2)
    {
        var bytes = bits / 8;
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);
        var dataLength = frames * channels * bytes;
        var extensible = channels > 2;
        writer.Write("RIFF"u8); writer.Write((extensible ? 60 : 36) + dataLength + (dataLength & 1)); writer.Write("WAVEfmt "u8);
        writer.Write(extensible ? 40 : 16); writer.Write(extensible ? (ushort)0xfffe : (ushort)1); writer.Write((short)channels); writer.Write(rate);
        writer.Write(rate * channels * bytes); writer.Write((short)(channels * bytes)); writer.Write((short)bits);
        if (extensible)
        {
            writer.Write((ushort)22); writer.Write((ushort)bits);
            writer.Write(channels switch { 3 => 0x7u, 4 => 0x107u, 5 => 0x37u, 6 => 0x3fu, 7 => 0x13fu, 8 => 0xffu, _ => throw new InvalidDataException("Definir a máscara do fixture multicanal.") });
            writer.Write(new Guid("00000001-0000-0010-8000-00aa00389b71").ToByteArray());
        }
        writer.Write("data"u8); writer.Write(frames * channels * bytes);
        var maximum = (1 << (bits - 1)) - 1;
        for (var frame = 0; frame < frames; frame++)
        for (var channel = 0; channel < channels; channel++)
        {
            var sample = frame >= frames - trailingSilentFrames ? 0
                : frame % 257 == 0 ? (channel == 0 ? maximum : -maximum - 1)
                : (int)(((long)frame * (channel == 0 ? 7919 : 1237 * channel)) % maximum) - maximum / 2;
            for (var b = 0; b < bytes; b++) writer.Write((byte)(sample >> (8 * b)));
        }
        if ((dataLength & 1) != 0) writer.Write((byte)0);
        writer.Flush();
        return memory.ToArray();
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
}
