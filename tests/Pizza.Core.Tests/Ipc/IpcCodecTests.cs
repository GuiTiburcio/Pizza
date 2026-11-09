using System.Text;
using Pizza.Core.Ipc;

namespace Pizza.Core.Tests.Ipc;

/// <summary>SEC-13: JSON estrito, tamanho máximo, esquema validado, campos desconhecidos rejeitados.</summary>
public class IpcCodecTests
{
    private const string Job = "\"v\":1,\"jobId\":\"0123456789ab\"";

    private static IpcMessage Parse(string json) => IpcCodec.Parse(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Le_um_convert_valido()
    {
        var message = Parse("{" + Job + ""","type":"convert","converterId":"image.png","srcHandle":120,"dstHandle":124,"options":{"compression":"balanced"}}""");

        Assert.Equal(IpcType.Convert, message.Type);
        Assert.Equal("0123456789ab", message.JobId);
        Assert.Equal("image.png", message.ConverterId);
        Assert.Equal(120, message.SrcHandle);
        Assert.Equal(124, message.DstHandle);
        Assert.Equal("balanced", message.Options!["compression"]);
    }

    [Theory]
    [InlineData("""{"type":"progress","percent":50}""", IpcType.Progress)]
    [InlineData("""{"type":"done","elapsedMs":182}""", IpcType.Done)]
    [InlineData("""{"type":"error","code":"UNSUPPORTED"}""", IpcType.Error)]
    [InlineData("""{"type":"cancel"}""", IpcType.Cancel)]
    [InlineData("""{"type":"warmup","category":"image"}""", IpcType.Warmup)]
    public void Le_os_demais_tipos(string body, IpcType expected)
    {
        var message = Parse("{" + Job + "," + body[1..]);

        Assert.Equal(expected, message.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("não é json")]
    [InlineData("[]")]
    [InlineData("""{"v":1}""")]
    [InlineData("""{"v":2,"jobId":"0123456789ab","type":"cancel"}""")] // versão desconhecida
    [InlineData("""{"v":1,"jobId":"\uD800","type":"cancel"}""")] // Unicode inválido
    [InlineData("""{"\uD800":1}""")]
    [InlineData("""{"v":1,"jobId":"xyz","type":"cancel"}""")] // jobId fora do formato
    [InlineData("""{"v":1,"jobId":"0123456789AB","type":"cancel"}""")] // só minúsculas
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"outro"}""")] // tipo desconhecido
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"cancel","extra":1}""")] // campo desconhecido
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"cancel","percent":1}""")] // campo de outro tipo
    [InlineData("""{"v":1,"v":1,"jobId":"0123456789ab","type":"cancel"}""")] // campo repetido
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"cancel"} {}""")] // lixo depois do objeto
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"progress","percent":101}""")]
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"progress","percent":-1}""")]
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"progress"}""")] // campo obrigatório ausente
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"done","elapsedMs":-5}""")]
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"error","code":"INVENTADO"}""")]
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"warmup","category":"audio"}""")]
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"convert","converterId":"../x","srcHandle":1,"dstHandle":2,"options":{}}""")]
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"convert","converterId":"image.png","srcHandle":0,"dstHandle":2,"options":{}}""")]
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"convert","converterId":"image.png","srcHandle":"1","dstHandle":2,"options":{}}""")]
    [InlineData("""{"v":1,"jobId":"0123456789ab","type":"convert","converterId":"image.png","srcHandle":1,"dstHandle":2,"options":{"a":1}}""")]
    public void Rejeita_mensagem_invalida(string json)
    {
        Assert.Throws<IpcProtocolException>(() => Parse(json));
    }

    [Fact]
    public void Caminho_de_arquivo_nao_tem_campo_no_contrato()
    {
        // SEC-15: o Worker só recebe handles.
        Assert.Throws<IpcProtocolException>(() => Parse("{" + Job + ""","type":"convert","converterId":"image.png","source":"C:\\a.jpg","srcHandle":1,"dstHandle":2,"options":{}}"""));
    }

    [Fact]
    public void Rejeita_mensagem_acima_de_64_KB()
    {
        var payload = new byte[IpcCodec.MaxMessageBytes + 1];

        Assert.Throws<IpcProtocolException>(() => IpcCodec.Parse(payload));
    }

    [Fact]
    public void Ida_e_volta_mantem_o_conteudo()
    {
        var original = new IpcMessage(IpcType.Convert, "abcdef012345")
        {
            ConverterId = "image.png",
            SrcHandle = 5000,
            DstHandle = 5004,
            Options = new Dictionary<string, string> { ["resolution"] = "original" },
        };

        var copy = IpcCodec.Parse(IpcCodec.Serialize(original));

        Assert.Equal(original.ConverterId, copy.ConverterId);
        Assert.Equal(original.SrcHandle, copy.SrcHandle);
        Assert.Equal(original.DstHandle, copy.DstHandle);
        Assert.Equal("original", copy.Options!["resolution"]);
    }

    [Fact]
    public void Quadro_le_uma_mensagem_por_linha()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc\ndef\n"));

        Assert.Equal("abc", Encoding.UTF8.GetString(IpcFraming.ReadFrame(stream)!));
        Assert.Equal("def", Encoding.UTF8.GetString(IpcFraming.ReadFrame(stream)!));
        Assert.Null(IpcFraming.ReadFrame(stream));
    }

    [Fact]
    public void Quadro_grande_demais_e_recusado_sem_ler_tudo()
    {
        using var stream = new MemoryStream(new byte[IpcCodec.MaxMessageBytes * 2]);

        Assert.Throws<IpcProtocolException>(() => IpcFraming.ReadFrame(stream));
    }

    [Fact]
    public void Quadro_cortado_no_meio_e_recusado()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sem fim de linha"));

        Assert.Throws<IpcProtocolException>(() => IpcFraming.ReadFrame(stream));
    }
}
