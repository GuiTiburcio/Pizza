using System.Buffers.Binary;

namespace Pizza.Converters.Tests;

public partial class AudioConverterTests
{
    [MediaTheory]
    [InlineData("audio.flac", 16)]
    [InlineData("audio.flac", 24)]
    [InlineData("audio.m4a", 16)]
    [InlineData("audio.m4a", 24)]
    [InlineData("audio.wav", 16)]
    [InlineData("audio.wav", 24)]
    public void Sem_perda_preserva_cada_amostra_e_a_ordem_dos_canais(string id, int bits)
    {
        // TST-02/TST-10: tamanho/duração iguais não provam fidelidade. Os canais diferem e incluem valores extremos.
        var source = PatternWave(bits);
        var before = source.ToArray();
        var options = id == "audio.m4a" ? new[] { ("bitrate", "lossless") } : Array.Empty<(string, string)>();
        var (error, encoded) = Run(id, source, options);
        Assert.Null(error);
        var (decodeError, decoded) = Run("audio.wav", encoded);
        Assert.Null(decodeError);
        Assert.Equal(source, before);
        Assert.Equal(48000, Inspect(decoded).SampleRate);
        Assert.Equal(2, Inspect(decoded).Channels);
        Assert.Equal(bits, Inspect(decoded).Bits);
        Assert.Equal(Pcm(source), Pcm(decoded));
    }

    private static byte[] PatternWave(int bits)
    {
        const int rate = 48000, frames = rate, channels = 2;
        var bytes = bits / 8;
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);
        writer.Write("RIFF"u8); writer.Write(36 + frames * channels * bytes); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)channels); writer.Write(rate);
        writer.Write(rate * channels * bytes); writer.Write((short)(channels * bytes)); writer.Write((short)bits);
        writer.Write("data"u8); writer.Write(frames * channels * bytes);
        var maximum = (1 << (bits - 1)) - 1;
        for (var frame = 0; frame < frames; frame++)
        for (var channel = 0; channel < channels; channel++)
        {
            var sample = frame % 257 == 0 ? (channel == 0 ? maximum : -maximum - 1)
                : (int)(((long)frame * (channel == 0 ? 7919 : 1237)) % maximum) - maximum / 2;
            for (var b = 0; b < bytes; b++) writer.Write((byte)(sample >> (8 * b)));
        }
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
