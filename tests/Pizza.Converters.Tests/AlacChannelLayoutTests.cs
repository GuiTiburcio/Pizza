using Pizza.Converters.Audio;

namespace Pizza.Converters.Tests;

public class AlacChannelLayoutTests
{
    [Theory]
    [InlineData(0x3fu)]
    [InlineData(0x60fu)]
    public void Cinco_ponto_um_com_back_ou_side_usa_o_layout_Apple_124(uint mask)
    {
        var layout = AlacChannelLayout.Find(6, mask);
        Assert.NotNull(layout);
        Assert.Equal((124u << 16) | 6, layout.Tag);
        Assert.Equal([2, 0, 1, 4, 5, 3], layout.Order);
    }

    [Theory]
    [InlineData(6, 0u)]
    [InlineData(6, 0x60eu)]
    [InlineData(6, 0x1u)]
    [InlineData(5, 0x60fu)]
    public void Multicanal_sem_mascara_ou_com_mascara_desconhecida_continua_recusado(int channels, uint mask) =>
        Assert.Null(AlacChannelLayout.Find(channels, mask));
}
