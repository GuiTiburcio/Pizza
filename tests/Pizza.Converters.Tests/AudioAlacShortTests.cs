using System.Buffers.Binary;

namespace Pizza.Converters.Tests;

public partial class AudioConverterTests
{
    [MediaFact]
    public void Alac_32_bits_multicanal_nao_reduz_para_16_antes_de_gravar_24()
    {
        var values = new[] { 0x12345600, -0x23456700, 0x34567800, -0x45678900, 0x56789a00, -0x6789ab00 };
        var wave24 = PatternWave(24, 48000, 1, 0, 6);
        var wave32 = new byte[68 + 24];
        wave24.AsSpan(0, 68).CopyTo(wave32);
        BinaryPrimitives.WriteInt32LittleEndian(wave32.AsSpan(4), wave32.Length - 8);
        BinaryPrimitives.WriteInt32LittleEndian(wave32.AsSpan(28), 48000 * 24);
        BinaryPrimitives.WriteUInt16LittleEndian(wave32.AsSpan(32), 24);
        BinaryPrimitives.WriteUInt16LittleEndian(wave32.AsSpan(34), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(wave32.AsSpan(38), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(wave32.AsSpan(64), 24);
        for (var i = 0; i < 6; i++) BinaryPrimitives.WriteInt32LittleEndian(wave32.AsSpan(68 + i * 4), values[i]);
        var (encodeError, container) = Run("audio.m4a", wave24, ("bitrate", "lossless"));
        Assert.Null(encodeError);
        var outer = container.AsSpan().IndexOf("alac"u8);
        var inner = outer + 4 + container.AsSpan(outer + 4).IndexOf("alac"u8);
        BinaryPrimitives.WriteUInt16BigEndian(container.AsSpan(outer + 22), 32);
        container[inner + 13] = 32; // ALACSpecificConfig.bitDepth
        var packet = new byte[52]; // quatro cabeçalhos de55bits +6 amostras32bits + END3bits.
        var bit = 0;
        var apple = new[] { values[2], values[0], values[1], values[4], values[5], values[3] };
        var channel = 0;
        foreach (var group in new[] { (0u, 1), (1u, 2), (1u, 2), (0u, 1) })
        {
            Write(group.Item1, 3); Write(0, 4); Write(0, 12); Write(9, 4); Write(1, 32);
            for (var i = 0; i < group.Item2; i++) Write(unchecked((uint)apple[channel++]), 32);
        }
        Write(7, 3);
        BinaryPrimitives.WriteUInt32BigEndian(container.AsSpan(inner + 20), (uint)packet.Length);
        container = ReplaceAlacPacket(container, packet);
        var (expectedError, expected) = Run("audio.wav", wave32);
        var (actualError, actual) = Run("audio.wav", container);
        Assert.Null(expectedError);
        Assert.Null(actualError);
        Assert.Equal(24, Inspect(actual).Bits);
        Assert.Equal(Pcm(expected), Pcm(actual));
        void Write(uint value, int count)
        {
            for (var shift = count - 1; shift >= 0; shift--, bit++) packet[bit / 8] |= (byte)(((value >> shift) & 1) << (7 - bit % 8));
        }
    }

    [MediaTheory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    public void Alac_para_flac_preserva_layout_ou_recusa_sem_mistura_silenciosa(int channels, bool unsupported)
    {
        var source = PatternWave(24, 48000, 4097, 7, channels);
        var (encodeError, alac) = Run("audio.m4a", source, ("bitrate", "lossless"));
        Assert.Null(encodeError);
        var (error, flac) = Run("audio.flac", alac);
        if (unsupported)
        {
            Assert.Equal("UNSUPPORTED", error);
            Assert.Empty(flac);
            var (stereoError, stereo) = Run("audio.flac", alac, ("channels", "stereo"));
            Assert.Null(stereoError);
            Assert.Equal(2, Inspect(stereo).Channels);
            return;
        }
        Assert.Null(error);
        var (decodeError, decoded) = Run("audio.wav", flac);
        Assert.Null(decodeError);
        Assert.Equal(Inspect(source).ChannelMask, Inspect(decoded).ChannelMask);
        Assert.Equal(Pcm(source), Pcm(decoded));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    public void Permutacao_Apple_para_Wave_preserva_bits_de_cada_canal(int bits)
    {
        var layout = Pizza.Converters.Audio.AlacChannelLayout.Find(6, 0x3f)!;
        var bytes = bits / 8;
        var wave = Enumerable.Range(0, 6 * bytes * 3).Select(value => unchecked((byte)(value * 31))).ToArray();
        var apple = new byte[wave.Length];
        for (var frame = 0; frame < wave.Length; frame += 6 * bytes)
        for (var channel = 0; channel < 6; channel++)
            wave.AsSpan(frame + layout.Order[channel] * bytes, bytes).CopyTo(apple.AsSpan(frame + channel * bytes, bytes));
        layout.ToWave(apple, bits);
        Assert.Equal(wave, apple);
        Assert.Throws<InvalidDataException>(() => layout.ToWave(new byte[5], bits));
    }

    [MediaTheory]
    [InlineData("mono", "48000", "16", "off")]
    [InlineData("stereo", "44100", "24", "off")]
    [InlineData("mono", "44100", "16", "-14")]
    [InlineData("stereo", "48000", "24", "-14")]
    public void Alac_51_mistura_reamostra_e_normaliza_como_o_PCM_original(string channels, string rate, string depth, string normalize)
    {
        var source = PatternWave(24, 48000, 48017, 17, 6);
        var (encodeError, alac) = Run("audio.m4a", source, ("bitrate", "lossless"));
        Assert.Null(encodeError);
        var options = new[] { ("channels", channels), ("rate", rate), ("depth", depth), ("normalize", normalize) };
        var (waveError, expected) = Run("audio.wav", source, options);
        var (alacError, actual) = Run("audio.wav", alac, options);
        Assert.Null(waveError);
        Assert.Null(alacError);
        Assert.Equal(Pcm(expected), Pcm(actual));
        // O catálogo M4A conserva a profundidade da origem; não oferece a opção depth.
        var nativeOptions = options.Where(option => option.Item1 != "depth").ToArray();
        var (expectedNativeError, expectedNative) = Run("audio.wav", source, [.. nativeOptions, ("depth", "24")]);
        Assert.Null(expectedNativeError);
        var (reencodeError, reencoded) = Run("audio.m4a", alac, [.. nativeOptions, ("bitrate", "lossless")]);
        Assert.Null(reencodeError);
        var (decodeError, decoded) = Run("audio.wav", reencoded);
        Assert.Null(decodeError);
        Assert.Equal(Pcm(expectedNative), Pcm(decoded));
    }

    [MediaTheory]
    [InlineData(16, 1, 1)]
    [InlineData(24, 2, 4097)]
    [InlineData(24, 6, 4097)]
    public void Alac_96kHz_preserva_taxa_e_amostras(int bits, int channels, int frames) =>
        AssertLosslessRoundtrip("audio.m4a", bits, 96000, frames, 0, channels);

    [MediaTheory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    public void Alac_preserva_layouts_multicanal_definidos_pela_Apple(int channels) =>
        AssertLosslessRoundtrip("audio.m4a", 24, 48000, 4097, 7, channels);

    [MediaFact]
    public void Alac_recusa_mascara_ou_cookie_ambiguos_sem_rebatizar_alto_falantes()
    {
        var source = PatternWave(16, 48000, 4097, 0, 6);
        var sideSurround = source.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(sideSurround.AsSpan(40, 4), 0x60f);
        // 5.1 com side surround (0x60F) é o mesmo layout Apple do 5.1 com back: os pares side e back viram Ls/Rs.
        var (sideError, sideEncoded) = Run("audio.m4a", sideSurround, ("bitrate", "lossless"));
        Assert.Null(sideError);
        var (sideDecodeError, sideDecoded) = Run("audio.wav", sideEncoded);
        Assert.Null(sideDecodeError);
        Assert.Equal(Pcm(source), Pcm(sideDecoded));
        var ambiguous = source.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(ambiguous.AsSpan(40, 4), 0x60e);
        var (maskError, rejected) = Run("audio.m4a", ambiguous, ("bitrate", "lossless"));
        Assert.Equal("UNSUPPORTED", maskError);
        Assert.Empty(rejected);
        var (error, alac) = Run("audio.m4a", source, ("bitrate", "lossless"));
        Assert.Null(error);
        var chan = alac.AsSpan().IndexOf("chan"u8);
        BinaryPrimitives.WriteUInt32BigEndian(alac.AsSpan(chan + 8, 4), (121u << 16) | 6);
        var (cookieError, output) = Run("audio.wav", alac);
        Assert.Equal("UNSUPPORTED", cookieError);
        Assert.Empty(output);
    }

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

    [MediaFact]
    public void Alac_sem_perdas_fora_dos_limites_do_encoder_e_nao_suportado()
    {
        var (error, _) = Run("audio.m4a", PatternWave(16, 6000, 4096, 0), ("bitrate", "lossless"));
        Assert.Equal("UNSUPPORTED", error);
    }
}