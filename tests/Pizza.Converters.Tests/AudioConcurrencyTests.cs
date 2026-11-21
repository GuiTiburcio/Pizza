namespace Pizza.Converters.Tests;

/// <summary>
/// O xUnit roda classes de teste em paralelo e o Worker pode atender conversões simultâneas: o ciclo
/// MFStartup/MFShutdown de cada conversão não pode derrubar a de outra thread (falha intermitente no CI).
/// </summary>
public class AudioConcurrencyTests
{
    [MediaFact]
    public void Conversoes_concorrentes_nao_falham_nem_corrompem()
    {
        var wav = TestAudio.Sine(seconds: 1, channels: 2, rate: 48000);
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 8 * 40, new ParallelOptions { MaxDegreeOfParallelism = 8 }, index =>
        {
            var id = (index % 3) switch { 0 => "audio.m4a", 1 => "audio.flac", _ => "audio.wav" };
            var info = Core.Conversions.ConverterCatalog.Find(id)!;
            var options = id == "audio.m4a" ? new Dictionary<string, string> { ["bitrate"] = "lossless" } : [];
            var resolved = Core.Conversions.ConverterCatalog.ResolveOptions(info, options)!;
            using var input = new MemoryStream(wav);
            using var output = new MemoryStream();
            var error = ConverterRegistry.Find(id)!.Convert(input, output, resolved);
            if (error is not null)
            {
                failures.Add($"{id}: {error}");
            }

            using var again = new MemoryStream(wav);
            if (Pizza.Converters.Audio.AudioFile.Inspect(again) is null)
            {
                failures.Add($"{id}: Inspect nulo");
            }
        });

        Assert.Empty(failures);
    }
}
