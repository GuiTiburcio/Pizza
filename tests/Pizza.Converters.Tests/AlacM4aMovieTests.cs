using System.Buffers.Binary;
using Pizza.Converters.Audio;

namespace Pizza.Converters.Tests;

public class AlacM4aMovieTests
{
    [Theory]
    [InlineData(1000L, 0)]
    [InlineData(uint.MaxValue, 0)]
    [InlineData(uint.MaxValue + 1L, 1)]
    [InlineData(10_000_000_000L, 1)]
    public void Duracao_acima_de_32_bits_usa_caixas_versao_1(long frames, int version)
    {
        var moov = AlacM4aWriter.Movie(new byte[24], 48000, 2, 16, AlacChannelLayout.Find(2, 3)!, frames, [], 40);
        var mvhd = Payload(moov, "mvhd");
        var tkhd = Payload(moov, "tkhd");
        var mdhd = Payload(moov, "mdhd");
        Assert.Equal(version, mvhd[0]);
        Assert.Equal(version, tkhd[0]);
        Assert.Equal(version, mdhd[0]);
        if (version == 1)
        {
            Assert.Equal(112, mvhd.Length);
            Assert.Equal(96, tkhd.Length);
            Assert.Equal(36, mdhd.Length);
            Assert.Equal(48000u, BinaryPrimitives.ReadUInt32BigEndian(mvhd.AsSpan(20)));
            Assert.Equal((ulong)frames, BinaryPrimitives.ReadUInt64BigEndian(mvhd.AsSpan(24)));
            Assert.Equal(0x10000u, BinaryPrimitives.ReadUInt32BigEndian(mvhd.AsSpan(32)));
            Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(mvhd.AsSpan(108)));
            Assert.Equal(1u, BinaryPrimitives.ReadUInt32BigEndian(tkhd.AsSpan(20)));
            Assert.Equal((ulong)frames, BinaryPrimitives.ReadUInt64BigEndian(tkhd.AsSpan(28)));
            Assert.Equal(0x100, BinaryPrimitives.ReadUInt16BigEndian(tkhd.AsSpan(48)));
            Assert.Equal(48000u, BinaryPrimitives.ReadUInt32BigEndian(mdhd.AsSpan(20)));
            Assert.Equal((ulong)frames, BinaryPrimitives.ReadUInt64BigEndian(mdhd.AsSpan(24)));
            Assert.Equal(0x55c4, BinaryPrimitives.ReadUInt16BigEndian(mdhd.AsSpan(32)));
        }
        else
        {
            Assert.Equal((uint)frames, BinaryPrimitives.ReadUInt32BigEndian(mvhd.AsSpan(16)));
            Assert.Equal((uint)frames, BinaryPrimitives.ReadUInt32BigEndian(tkhd.AsSpan(20)));
            Assert.Equal((uint)frames, BinaryPrimitives.ReadUInt32BigEndian(mdhd.AsSpan(16)));
        }
    }

    private static byte[] Payload(byte[] data, string name)
    {
        var index = data.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes(name));
        Assert.True(index >= 4);
        var size = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(index - 4));
        return data.AsSpan(index + 4, size - 8).ToArray();
    }
}
