using System.Buffers.Binary;

namespace Pizza.Converters.Tests;

public partial class AudioConverterTests
{
    [MediaFact]
    public unsafe void Alac_51_grava_cada_alto_falante_na_posicao_definida_pelo_formato()
    {
        using var wave = new MemoryStream();
        using (var writer = new BinaryWriter(wave, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("RIFF"u8); writer.Write(72); writer.Write("WAVEfmt "u8); writer.Write(40);
            writer.Write((ushort)0xfffe); writer.Write((ushort)6); writer.Write(48000); writer.Write(576000);
            writer.Write((ushort)12); writer.Write((ushort)16); writer.Write((ushort)22); writer.Write((ushort)16);
            writer.Write(0x3fu); writer.Write(new Guid("00000001-0000-0010-8000-00aa00389b71").ToByteArray());
            writer.Write("data"u8); writer.Write(12);
            foreach (var value in new short[] { 1000, 2000, 3000, 4000, 5000, 6000 }) writer.Write(value);
        }
        var (error, encoded) = Run("audio.m4a", wave.ToArray(), ("bitrate", "lossless"));
        Assert.Null(error);
        var packet = SingleAlacPacket(encoded);
        var bit = 0;
        uint Read(int count)
        {
            uint value = 0;
            for (var i = 0; i < count; i++, bit++)
            {
                Assert.True(bit < packet.Length * 8);
                value = (value << 1) | (uint)((packet[bit / 8] >> (7 - bit % 8)) & 1);
            }
            return value;
        }
        var actual = new List<short>();
        // Um quadro não compensa predição: verifica PCM escape sem usar o decoder Media Foundation.
        foreach (var element in new[] { (0u, 1), (1u, 2), (1u, 2), (0u, 1) })
        {
            Assert.Equal(element.Item1, Read(3));
            Read(4); Assert.Equal(0u, Read(12)); Assert.Equal(9u, Read(4)); Assert.Equal(1u, Read(32));
            for (var channel = 0; channel < element.Item2; channel++) actual.Add(unchecked((short)Read(16)));
        }
        Assert.Equal(7u, Read(3));
        Assert.Equal(new short[] { 3000, 1000, 2000, 5000, 6000, 4000 }, actual); // C,L,R,Ls,Rs,LFE
        Pizza.Converters.Audio.MediaFoundation.Startup();
        try
        {
            using var input = new MemoryStream(encoded);
            using var reader = Pizza.Converters.Audio.AudioReader.Open(input);
            Pizza.Converters.Audio.MediaFoundation.NativeMediaType(reader!.Handle, 0, 0, out var type);
            try
            {
                var key = new Guid("b6bc765f-4c3b-40a4-bd51-2535b66fe09d");
                var data = stackalloc byte[256];
                uint length;
                var hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, byte*, uint, uint*, int>)(*(nint**)type)[15])(type, &key, data, 256, &length);
                Assert.Equal(0, hr);
                Assert.Equal(48u, length);
                Assert.Equal((124u << 16) | 6, BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(data + 36, 4)));
            }
            finally { Pizza.Converters.Audio.MediaFoundation.Release(type); }
        }
        finally { Pizza.Converters.Audio.MediaFoundation.Shutdown(); }
        var (decodeError, decoded) = Run("audio.wav", encoded);
        Assert.Null(decodeError);
        var pcm = Pcm(decoded);
        Assert.Equal(new short[] { 1000, 2000, 3000, 4000, 5000, 6000 },
            Enumerable.Range(0, 6).Select(index => BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(index * 2))).ToArray());
    }

    private static byte[] SingleAlacPacket(byte[] container)
    {
        for (var position = 0; position < container.Length;)
        {
            var size = BinaryPrimitives.ReadUInt32BigEndian(container.AsSpan(position, 4));
            var header = size == 1 ? 16 : 8;
            var length = size == 1 ? checked((int)BinaryPrimitives.ReadUInt64BigEndian(container.AsSpan(position + 8, 8))) : checked((int)size);
            Assert.InRange(length, header, container.Length - position);
            if (container.AsSpan(position + 4, 4).SequenceEqual("mdat"u8)) return container.AsSpan(position + header, length - header).ToArray();
            position += length;
        }
        throw new InvalidDataException("Fixture sem mdat.");
    }

    [MediaFact]
    public void Alac_trechos_curtos_preservam_pcm_em_concorrencia()
    {
        // #101: o êxito de milhares de rodadas isoladas não eliminou a falha na suíte concorrente.
        Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 8 }, index =>
        {
            var frames = new[] { 1, 7, 4095, 4096, 4097 }[index % 5];
            AssertLosslessRoundtrip("audio.m4a", index % 2 == 0 ? 16 : 24,
                index % 3 == 0 ? 44100 : 48000, frames, Math.Min(3, frames), index % 4 == 0 ? 1 : 2);
        });
    }
}
