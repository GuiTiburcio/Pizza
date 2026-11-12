using System.Buffers.Binary;
using System.Text;

namespace Pizza.Integration.Tests;

internal static class AudioSamples
{
    internal const int Rate = 44_100;
    internal const int Frames = Rate / 5;

    internal static byte[] Wave(bool silence = false)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(36 + (Frames * 4));
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((ushort)1); // PCM, 16 bits, estéreo.
        writer.Write((ushort)2);
        writer.Write(Rate);
        writer.Write(Rate * 4);
        writer.Write((ushort)4);
        writer.Write((ushort)16);
        writer.Write("data"u8);
        writer.Write(Frames * 4);
        for (var frame = 0; frame < Frames; frame++)
        {
            writer.Write(silence ? (short)0 : (short)(8191 * Math.Sin(2 * Math.PI * 440 * frame / Rate)));
            writer.Write(silence ? (short)0 : (short)(4095 * Math.Sin(2 * Math.PI * 880 * frame / Rate)));
        }
        return stream.ToArray();
    }

    internal static (int Channels, int Rate, int Bits, byte[] Data) ReadWave(byte[] bytes)
    {
        Assert.True(bytes.Length >= 12);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
        var channels = 0;
        var rate = 0;
        var bits = 0;
        byte[]? data = null;
        for (var offset = 12; offset <= bytes.Length - 8;)
        {
            var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));
            Assert.True(length <= bytes.Length - offset - 8, "Chunk WAV truncado.");
            var body = bytes.AsSpan(offset + 8, (int)length);
            if (bytes.AsSpan(offset, 4).SequenceEqual("fmt "u8))
            {
                Assert.True(length >= 16);
                var encoding = BinaryPrimitives.ReadUInt16LittleEndian(body);
                Assert.Contains(encoding, new ushort[] { 1, 0xfffe });
                channels = BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
                rate = BinaryPrimitives.ReadInt32LittleEndian(body[4..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(body[14..]);
                if (encoding == 0xfffe)
                {
                    Assert.True(length >= 40);
                    Assert.Equal(new Guid("00000001-0000-0010-8000-00aa00389b71"), new Guid(body[24..40]));
                }
            }
            else if (bytes.AsSpan(offset, 4).SequenceEqual("data"u8)) data = body.ToArray();
            offset += 8 + (int)length + ((int)length & 1);
        }
        Assert.NotNull(data);
        Assert.True(channels > 0 && rate > 0 && bits > 0);
        Assert.Equal(0, data.Length % (channels * bits / 8));
        return (channels, rate, bits, data);
    }
}

