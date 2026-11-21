namespace Pizza.Converters.Tests;
public partial class AudioConverterTests
{
    [MediaFact]
    public void ZStress()
    {
        var cases = new[] { (16, 1, 1), (16, 2, 4095), (16, 2, 4096), (24, 2, 4096), (16, 2, 8192), (16, 2, 4097) };
        var report = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
        var options = Core.Conversions.ConverterCatalog.ResolveOptions(Core.Conversions.ConverterCatalog.Find("audio.m4a")!, new Dictionary<string, string> { ["bitrate"] = "lossless" })!;
        var waves = cases.ToDictionary(c => c, c => PatternWave(c.Item1, 48000, c.Item3, 0, c.Item2));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Parallel.For(0, cases.Length * 300, new ParallelOptions { MaxDegreeOfParallelism = 16 }, n =>
        {
            var c = cases[n % cases.Length];
            using var input = new MemoryStream(waves[c]); using var output = new MemoryStream();
            string? error;
            try { error = ConverterRegistry.Find("audio.m4a")!.Convert(input, output, options); }
            catch (Exception ex) { error = ex.GetType().Name; }
            report.AddOrUpdate($"{c}", error is null ? 0 : 1, (_, v) => v + (error is null ? 0 : 1));
        });
        Assert.Fail($"variant={Pizza.Converters.Audio.Diag.Variant} ms={sw.ElapsedMilliseconds} " + string.Join(" ", report.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}/300")));
    }
}
