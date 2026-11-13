using System.Globalization;
using Pizza.Core.Ipc;
using Pizza.Core.Output;

namespace Pizza.Integration.Tests;

/// <summary>
/// TST-03: arquivos válidos, corrompidos de forma repetível, passando pelo Worker real em sandbox.
/// PIZZA_FUZZ_CASES e PIZZA_FUZZ_SEED ajustam a rodada; os casos reprovados ficam em TestResults/fuzz.
/// </summary>
[Collection(SandboxCollection.Name)]
public sealed class CorruptedFilesTests(SandboxFixture fixture) : IDisposable
{
    private const int DefaultCases = 100;
    private const long SeedStep = 1_000_000;

    private static readonly string[] AcceptedErrors = ["CORRUPTED", "UNSUPPORTED", "LIMIT_EXCEEDED"];

    private static readonly (string Id, string Extension)[] Targets =
    [
        ("image.jpg", "jpg"), ("image.png", "png"), ("image.webp", "webp"), ("image.avif", "avif"), ("image.pdf", "pdf"),
        ("image.tiff", "tiff"), ("image.ico", "ico"), ("image.gif", "gif"),
    ];

    private readonly string _folder = Directory.CreateTempSubdirectory("pizza-corrompidos-").FullName;

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
        File.Delete(_folder + ".journal");
    }

    [Fact]
    public void Arquivo_corrompido_nunca_derruba_trava_nem_deixa_resto()
    {
        var repository = RepositoryRoot();
        var corpus = Directory.GetFiles(Path.Combine(repository, "tests", "samples", "corpus")).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(6, corpus.Length);

        var cases = Number("PIZZA_FUZZ_CASES", DefaultCases);
        var firstSeed = Number("PIZZA_FUZZ_SEED", 0) * SeedStep;
        var runner = new ConversionRunner(fixture.WorkerPath, SandboxFixture.Profile, new TempJournal(_folder + ".journal"), TimeSpan.FromSeconds(30));
        var failures = new List<string>();

        for (long index = 0; index < cases; index++)
        {
            var seed = firstSeed + index;
            var random = new Random((int)(seed % int.MaxValue));
            var original = corpus[index % corpus.Length];
            var bytes = Mutate(File.ReadAllBytes(original), random);
            var target = Targets[random.Next(Targets.Length)];
            var path = Path.Combine(_folder, "caso" + Path.GetExtension(original));
            File.WriteAllBytes(path, bytes);

            var problem = Check(runner.Run(path, target.Id, target.Extension), path, bytes);
            if (problem is not null)
            {
                failures.Add($"semente {seed}, {Path.GetFileName(original)} para {target.Id}: {problem}");
                Keep(repository, path, seed);
            }

            Clear();
        }

        Assert.True(failures.Count == 0, $"{failures.Count} de {cases} casos reprovados:\n" + string.Join('\n', failures));
    }

    [MediaFact]
    public void Audio_corrompido_nunca_derruba_trava_nem_deixa_resto()
    {
        // ADR-035: o corpus de áudio é gerado aqui (WAV de teste convertido pelo próprio Worker), sem arquivo de usuário.
        var runner = new ConversionRunner(fixture.WorkerPath, SandboxFixture.Profile, new TempJournal(_folder + ".journal"), TimeSpan.FromSeconds(30));
        var corpus = AudioCorpus(runner);
        (string Id, string Extension)[] targets = [("audio.mp3", "mp3"), ("audio.m4a", "m4a"), ("audio.flac", "flac"), ("audio.wav", "wav")];
        var cases = Number("PIZZA_FUZZ_CASES", DefaultCases) / 2;
        var firstSeed = Number("PIZZA_FUZZ_SEED", 0) * SeedStep;
        var failures = new List<string>();

        for (long index = 0; index < cases; index++)
        {
            var seed = firstSeed + index;
            var random = new Random((int)(seed % int.MaxValue));
            var (extension, original) = corpus[(int)(index % corpus.Length)];
            var bytes = Mutate(original, random);
            var target = targets[random.Next(targets.Length)];
            var path = Path.Combine(_folder, "caso." + extension);
            File.WriteAllBytes(path, bytes);

            var problem = Check(runner.Run(path, target.Id, target.Extension), path, bytes);
            if (problem is not null)
            {
                failures.Add($"semente {seed}, {extension} para {target.Id}: {problem}");
                Keep(RepositoryRoot(), path, seed);
            }

            Clear();
        }

        Assert.True(failures.Count == 0, $"{failures.Count} de {cases} casos reprovados:\n" + string.Join('\n', failures));
    }

    private (string Extension, byte[] Bytes)[] AudioCorpus(ConversionRunner runner)
    {
        var wav = Path.Combine(_folder, "base.wav");
        File.WriteAllBytes(wav, AudioSample());
        var corpus = new List<(string, byte[])> { ("wav", File.ReadAllBytes(wav)) };
        foreach (var (id, extension) in new[] { ("audio.mp3", "mp3"), ("audio.m4a", "m4a"), ("audio.flac", "flac") })
        {
            var result = runner.Run(wav, id, extension);
            Assert.Null(result.ErrorCode);
            corpus.Add((extension, File.ReadAllBytes(Path.Combine(_folder, result.OutputName!))));
        }

        Clear();
        return corpus.ToArray();
    }

    private static byte[] AudioSample()
    {
        const int rate = 22050, frames = rate / 2;
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);
        writer.Write("RIFF"u8);
        writer.Write(36 + (frames * 2));
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(frames * 2);
        for (var i = 0; i < frames; i++)
        {
            writer.Write((short)(Math.Sin(2 * Math.PI * 330 * i / rate) * 9000));
        }

        writer.Flush();
        return memory.ToArray();
    }

    private string? Check(ConversionResult result, string path, byte[] bytes)
    {
        if (!bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
        {
            return "o original foi alterado";
        }

        var hidden = new EnumerationOptions { AttributesToSkip = 0 };
        if (Directory.GetFiles(_folder, ".~pizza-*", hidden).Length > 0)
        {
            return "sobrou arquivo temporário";
        }

        if (result.ErrorCode is { } code)
        {
            return AcceptedErrors.Contains(code) ? null : code;
        }

        var produced = Directory.GetFiles(_folder).Where(file => !string.Equals(file, path, StringComparison.OrdinalIgnoreCase)).ToArray();
        return produced.Length == 1 && new FileInfo(produced[0]).Length > 0 ? null : "conversão sem arquivo de saída";
    }

    private void Clear()
    {
        foreach (var file in Directory.GetFiles(_folder, "*", new EnumerationOptions { AttributesToSkip = 0 }))
        {
            File.Delete(file);
        }
    }

    private static void Keep(string repository, string path, long seed)
    {
        var folder = Path.Combine(repository, "TestResults", "fuzz");
        Directory.CreateDirectory(folder);
        File.Copy(path, Path.Combine(folder, $"caso-{seed}{Path.GetExtension(path)}"), overwrite: true);
    }

    private static byte[] Mutate(byte[] input, Random random)
    {
        var data = new List<byte>(input);
        for (var round = random.Next(1, 4); round > 0 && data.Count > 0; round--)
        {
            switch (random.Next(6))
            {
                case 0: // Inverte bits soltos.
                    for (var flips = random.Next(1, 33); flips > 0; flips--)
                    {
                        data[random.Next(data.Count)] ^= (byte)(1 << random.Next(8));
                    }

                    break;

                case 1: // Sobrescreve um trecho com lixo.
                    var start = random.Next(data.Count);
                    for (var offset = start; offset < Math.Min(data.Count, start + random.Next(1, 65)); offset++)
                    {
                        data[offset] = (byte)random.Next(256);
                    }

                    break;

                case 2: // Corta o final.
                    data = data.GetRange(0, random.Next(1, data.Count + 1));
                    break;

                case 3: // Estraga o cabeçalho, onde ficam tamanhos e deslocamentos.
                    var value = random.Next(3) switch { 0 => (byte)0x00, 1 => (byte)0xFF, _ => (byte)random.Next(256) };
                    for (var count = random.Next(1, 9); count > 0; count--)
                    {
                        data[random.Next(Math.Min(data.Count, 64))] = value;
                    }

                    break;

                case 4: // Insere bytes no meio.
                    var extra = new byte[random.Next(1, 129)];
                    random.NextBytes(extra);
                    data.InsertRange(random.Next(data.Count + 1), extra);
                    break;

                default: // Repete um trecho do próprio arquivo em outro lugar.
                    var from = random.Next(data.Count);
                    var chunk = data.GetRange(from, Math.Min(data.Count - from, random.Next(1, 257)));
                    data.InsertRange(random.Next(data.Count + 1), chunk);
                    break;
            }
        }

        return data.ToArray();
    }

    private static long Number(string name, long fallback) =>
        long.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : fallback;

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
