namespace Pizza.Converters.Tests;
public partial class AudioConverterTests
{
    [MediaFact]
    public void ZFormats()
    {
        var wave = PatternWave(16, 48000, 4096, 0, 2);
        var report = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
        var ids = new[] { ("audio.m4a", "lossless"), ("audio.m4a", "128"), ("audio.flac", ""), ("audio.wav", ""), ("audio.mp3", "128") };
        var resolved = ids.ToDictionary(i => i, i => Core.Conversions.ConverterCatalog.ResolveOptions(Core.Conversions.ConverterCatalog.Find(i.Item1)!, i.Item2 == "" ? [] : new Dictionary<string, string> { ["bitrate"] = i.Item2 })!);
        Parallel.For(0, ids.Length * 3000, new ParallelOptions { MaxDegreeOfParallelism = 16 }, n =>
        {
            var c = ids[n % ids.Length];
            using var input = new MemoryStream(wave); using var output = new MemoryStream();
            string? error;
            try { error = ConverterRegistry.Find(c.Item1)!.Convert(input, output, resolved[c]); }
            catch (Exception ex) { error = ex.GetType().Name; }
            report.AddOrUpdate($"{c.Item1}/{c.Item2}", error is null ? 0 : 1, (_, v) => v + (error is null ? 0 : 1));
        });
        Assert.Fail($"formats " + string.Join(" ", report.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}/3000")));
    }
}
