using System.Diagnostics;
using System.Text.Json;
using NetVips;
using Pizza.Core.Conversions;
using Image = NetVips.Image;

namespace Pizza.Converters.Tests;

/// <summary>
/// TST-14: confere os metadados com o ExifTool, um verificador independente do libvips.
/// O CI define PIZZA_EXIFTOOL; fora do CI, sem a ferramenta, estes testes não verificam nada.
/// </summary>
public sealed class ExifToolTests : IDisposable
{
    private static readonly string? Tool = Environment.GetEnvironmentVariable("PIZZA_EXIFTOOL");

    private static readonly string[] PersonalTags = ["Artist", "GPSLatitude", "GPSLongitude", "Creator"];

    private readonly string _folder = Directory.CreateTempSubdirectory("pizza-exif-").FullName;

    // Entram imagens com EXIF: só as conversões da aba Imagem. O PDF → PDF tem os próprios testes (TST-22).
    public static TheoryData<string> AllOutputs => new(ConverterCatalog.All.Where(converter => converter.Tab == ConverterCatalog.ImageTab).Select(converter => converter.Id));

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Theory]
    [MemberData(nameof(AllOutputs))]
    public void Por_padrao_a_saida_nao_tem_dados_pessoais(string converterId)
    {
        // PRIV-30.
        if (!ToolAvailable())
        {
            return;
        }

        var tags = ReadTags(Convert(converterId, keep: false));

        foreach (var tag in PersonalTags)
        {
            Assert.False(tags.TryGetProperty(tag, out _), $"{converterId} manteve {tag}");
        }
    }

    [Theory]
    [InlineData("image.jpg")]
    [InlineData("image.png")]
    [InlineData("image.webp")]
    [InlineData("image.avif")]
    public void Manter_metadados_preserva_autor_e_gps(string converterId)
    {
        // PRIV-32.
        if (!ToolAvailable())
        {
            return;
        }

        var tags = ReadTags(Convert(converterId, keep: true));

        Assert.Equal("Fulano de Tal", tags.GetProperty("Artist").GetString());
        Assert.True(tags.TryGetProperty("GPSLatitude", out _));
    }

    private static bool ToolAvailable()
    {
        Assert.False(Tool is null && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true", "O CI precisa definir PIZZA_EXIFTOOL.");
        return Tool is not null;
    }

    private string Convert(string converterId, bool keep)
    {
        var info = ConverterCatalog.Find(converterId)!;
        var given = keep ? new Dictionary<string, string> { [ConverterCatalog.MetadataKey] = ConverterCatalog.MetadataKeep } : [];
        var output = Path.Combine(_folder, "saida." + info.Extension);
        using (var input = File.OpenRead(TaggedSource()))
        using (var destination = File.Create(output))
        {
            Assert.Null(ConverterRegistry.Find(converterId)!.Convert(input, destination, ConverterCatalog.ResolveOptions(info, given)!));
        }

        return output;
    }

    private string TaggedSource()
    {
        var path = Path.Combine(_folder, "origem.jpg");
        using (var black = Image.Black(64, 64, bands: 3))
        using (var image = (black + new[] { 120.0, 130.0, 140.0 }).Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb))
        {
            image.Jpegsave(path);
        }

        Run("-overwrite_original", "-Artist=Fulano de Tal", "-GPSLatitude=23.5", "-GPSLatitudeRef=S", "-GPSLongitude=46.6", "-GPSLongitudeRef=W", "-XMP-dc:Creator=Fulano de Tal", path);

        // Sem isto, o teste passaria mesmo se a marcação não tivesse funcionado.
        var written = ReadTags(path);
        foreach (var tag in PersonalTags)
        {
            Assert.True(written.TryGetProperty(tag, out _), $"A origem não recebeu {tag}");
        }

        return path;
    }

    private static JsonElement ReadTags(string path)
    {
        using var document = JsonDocument.Parse(Run("-j", path));
        return document.RootElement[0].Clone();
    }

    private static string Run(params string[] arguments)
    {
        var start = new ProcessStartInfo(Tool!) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        _ = errors.Result;
        return output;
    }
}
