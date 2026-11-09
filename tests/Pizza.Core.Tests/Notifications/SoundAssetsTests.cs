using System.Buffers.Binary;
using System.Text;

namespace Pizza.Core.Tests.Notifications;

/// <summary>FR-43: sons leves em WAV, gerados por tools/gerar-sons.ps1.</summary>
public class SoundAssetsTests
{
    [Theory]
    [InlineData("concluido.wav")]
    [InlineData("erro.wav")]
    public void Som_e_um_wav_pcm_com_menos_de_meio_segundo(string name)
    {
        var wav = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "src", "Pizza.App", "Assets", name));

        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(20)));
        var byteRate = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(28));
        var dataBytes = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40));
        Assert.InRange(dataBytes / (double)byteRate, 0.05, 0.5);
    }

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(folder!.FullName, "Pizza.slnx")))
        {
            folder = folder.Parent;
        }

        return folder.FullName;
    }
}
