using Pizza.Core.Ipc;

namespace Pizza.Core.Tests.Ipc;

/// <summary>ADR-035 e SPECS §10.4: Windows sem Media Foundation recebe um erro próprio, com o que fazer.</summary>
public class MediaUnavailableTests
{
    [Fact]
    public void Codigo_passa_pelo_canal_e_tem_mensagem_propria()
    {
        var message = new IpcMessage(IpcType.Error, "0123456789ab") { Code = "MEDIA_UNAVAILABLE" };

        var parsed = IpcCodec.Parse(IpcCodec.Serialize(message));

        Assert.Equal("MEDIA_UNAVAILABLE", parsed.Code);
        Assert.Contains("Media Feature Pack", ConversionMessages.For("MEDIA_UNAVAILABLE"));
    }
}
