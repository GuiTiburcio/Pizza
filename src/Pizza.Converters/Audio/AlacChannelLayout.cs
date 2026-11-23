namespace Pizza.Converters.Audio;

/// <summary>Layouts definidos pela referência Apple. Não interpretar quad ou 7.1 comum como outro layout.</summary>
internal sealed record AlacChannelLayout(uint Mask, uint Tag, int[] Order)
{
    internal void ToWave(Span<byte> pcm, int bits)
    {
        var sampleBytes = bits / 8;
        var frameBytes = checked(Order.Length * sampleBytes);
        if (bits is not (16 or 24) || pcm.Length % frameBytes != 0) throw new InvalidDataException("PCM ALAC desalinhado.");
        Span<byte> frame = stackalloc byte[24];
        for (var offset = 0; offset < pcm.Length; offset += frameBytes)
        {
            pcm.Slice(offset, frameBytes).CopyTo(frame);
            for (var apple = 0; apple < Order.Length; apple++)
                frame.Slice(apple * sampleBytes, sampleBytes).CopyTo(pcm.Slice(offset + Order[apple] * sampleBytes, sampleBytes));
        }
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(frame);
    }

    internal static AlacChannelLayout? FromTag(int channels, uint tag)
    {
        var mask = channels switch { 3 => 0x7u, 4 => 0x107u, 5 => 0x37u, 6 => 0x3fu, 7 => 0x13fu, 8 => 0xffu, _ => 0u };
        var layout = Find(channels, mask);
        return layout?.Tag == tag ? layout : null;
    }

    internal static AlacChannelLayout? Find(int channels, uint mask)
    {
        var layout = channels switch
        {
            1 => new AlacChannelLayout(0x4, (100u << 16) | 1, [0]),
            2 => new AlacChannelLayout(0x3, (101u << 16) | 2, [0, 1]),
            3 => new AlacChannelLayout(0x7, (113u << 16) | 3, [2, 0, 1]),
            4 => new AlacChannelLayout(0x107, (116u << 16) | 4, [2, 0, 1, 3]),
            5 => new AlacChannelLayout(0x37, (120u << 16) | 5, [2, 0, 1, 3, 4]),
            6 => new AlacChannelLayout(0x3f, (124u << 16) | 6, [2, 0, 1, 4, 5, 3]),
            7 => new AlacChannelLayout(0x13f, (142u << 16) | 7, [2, 0, 1, 4, 5, 6, 3]),
            8 => new AlacChannelLayout(0xff, (127u << 16) | 8, [2, 6, 7, 0, 1, 4, 5, 3]),
            _ => null,
        };
        // 5.1 com side surround (0x60F) tem o mesmo layout Apple do 5.1 com back (0x3F): pares side e back viram Ls/Rs.
        var sideSurround51 = channels == 6 && mask == 0x60f;
        // Mono marcado como esquerdo (FL, 0x1) é o mesmo canal único do layout Apple mono.
        var monoLeft = channels == 1 && mask == 0x1;
        return layout is not null && (mask == layout.Mask || sideSurround51 || monoLeft || (mask == 0 && channels <= 2)) ? layout : null;
    }
}
