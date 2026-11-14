using System.Text;
using Pizza.Core.Ipc;

namespace Pizza.Core.Tests.Ipc;

/// <summary>SPECS §10.3 (adendo do ADR-035): "probe" e "info", no mesmo esquema estrito das outras mensagens (SEC-13).</summary>
public class ProbeMessagesTests
{
    private const string Job = "0123456789ab";

    [Fact]
    public void Probe_e_info_passam_pelo_canal()
    {
        var probe = IpcCodec.Parse(IpcCodec.Serialize(new IpcMessage(IpcType.Probe, Job) { ConverterId = "audio.mp3", SrcHandle = 1234 }));
        var info = IpcCodec.Parse(IpcCodec.Serialize(new IpcMessage(IpcType.Info, Job) { Media = new MediaInfo(192_000, 44100, 2, 16, 42) }));

        Assert.Equal(("audio.mp3", 1234L), (probe.ConverterId, probe.SrcHandle));
        Assert.Equal(new MediaInfo(192_000, 44100, 2, 16, 42), info.Media);
    }

    [Theory]
    [InlineData("""{"v":1,"type":"probe","jobId":"0123456789ab","converterId":"audio.mp3","srcHandle":5,"path":"C:\\x"}""")]
    [InlineData("""{"v":1,"type":"probe","jobId":"0123456789ab","converterId":"audio.mp3"}""")]
    [InlineData("""{"v":1,"type":"info","jobId":"0123456789ab","durationMs":-1,"sampleRate":44100,"channels":2,"bits":16,"losslessPercent":40}""")]
    [InlineData("""{"v":1,"type":"info","jobId":"0123456789ab","durationMs":1000,"sampleRate":0,"channels":2,"bits":16,"losslessPercent":40}""")]
    [InlineData("""{"v":1,"type":"info","jobId":"0123456789ab","durationMs":1000,"sampleRate":44100,"channels":99,"bits":16,"losslessPercent":40}""")]
    [InlineData("""{"v":1,"type":"info","jobId":"0123456789ab","durationMs":1000,"sampleRate":44100,"channels":2}""")]
    [InlineData("""{"v":1,"type":"info","jobId":"0123456789ab","durationMs":1000,"sampleRate":44100,"channels":2,"bits":16,"losslessPercent":101}""")]
    public void Fora_do_esquema_e_recusado(string json)
    {
        // Nenhum caminho de arquivo trafega (SEC-15); valores fora da faixa ou campos a mais derrubam a mensagem.
        Assert.Throws<IpcProtocolException>(() => IpcCodec.Parse(Encoding.UTF8.GetBytes(json)));
    }
}
