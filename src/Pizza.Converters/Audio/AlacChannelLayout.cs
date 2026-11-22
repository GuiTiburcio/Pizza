namespace Pizza.Converters.Audio;

/// <summary>Layouts definidos pela referência Apple. Não interpretar quad ou 7.1 comum como outro layout.</summary>
internal sealed record AlacChannelLayout(uint Mask, uint Tag, int[] Order)
{
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
        return layout is not null && (mask == layout.Mask || (mask == 0 && channels <= 2)) ? layout : null;
    }
}
