using System.Globalization;
using NetVips;
using Pizza.Core.Conversions;
using Image = NetVips.Image;

namespace Pizza.Converters;

/// <summary>Base das conversões de imagem: decodifica com a lista e os limites da SPECS. O arquivo de entrada não é confiável.</summary>
public abstract class ImageConverter : IConverter
{
    private const long MaxSourceBytes = 2L * 1024 * 1024 * 1024; // SEC-40
    private const long MaxPixels = 100_000_000; // SEC-41

    private static readonly string[] AllowedLoaders =
    [
        "VipsForeignLoadJpeg", "VipsForeignLoadPng", "VipsForeignLoadWebp",
        "VipsForeignLoadTiff", "VipsForeignLoadNsgif", "VipsForeignLoadHeif",
    ];

    static ImageConverter()
    {
        // SEC-45: bloqueia todos os decodificadores e libera só os da lista, uma vez por processo.
        global::NetVips.NetVips.BlockUntrusted = true;
        Operation.Block("VipsForeignLoad", true);
        foreach (var loader in AllowedLoaders)
        {
            Operation.Block(loader, false);
        }
    }

    public abstract string Id { get; }

    public string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null)
    {
        if (source.Length > MaxSourceBytes)
        {
            return "LIMIT_EXCEEDED";
        }

        try
        {
            if (Image.FindLoadStream(source) is null)
            {
                return "UNSUPPORTED";
            }

            source.Seek(0, SeekOrigin.Begin);
            using var header = Image.NewFromStream(source, access: Enums.Access.Sequential, failOn: Enums.FailOn.Error);
            if ((long)header.Width * header.Height > MaxPixels)
            {
                return "LIMIT_EXCEEDED";
            }

            // ADR-029: as etapas seguintes herdam o aviso de andamento da imagem de entrada.
            Track(header, progress);
            WriteUpright(header, source, destination, options, progress);
            return null;
        }
        catch (VipsException)
        {
            return "CORRUPTED";
        }
    }

    /// <summary>Grava a imagem já na posição certa. PRIV-30 e PRIV-31: só o perfil de cor pode ser mantido.</summary>
    protected abstract void Encode(Image image, Stream destination, IReadOnlyDictionary<string, string> options);

    /// <summary>Para formatos que não guardam perfil de cor: aplica o perfil embutido, ou as cores mudariam (PRIV-31).</summary>
    protected static Image ToSrgb(Image image) =>
        image.Contains("icc-profile-data") ? image.IccTransform("srgb", embedded: true) : image.Colourspace(Enums.Interpretation.Srgb);

    /// <summary>PRIV-30 e PRIV-32: só o perfil de cor, a não ser que o usuário marque "Manter metadados".</summary>
    protected static Enums.ForeignKeep Keep(IReadOnlyDictionary<string, string> options) =>
        options[ConverterCatalog.MetadataKey] == ConverterCatalog.MetadataKeep ? Enums.ForeignKeep.All : Enums.ForeignKeep.Icc;

    protected static int Number(IReadOnlyDictionary<string, string> options, string key) =>
        int.Parse(options[key], CultureInfo.InvariantCulture);

    private static void Track(Image image, IProgress<int>? progress)
    {
        if (progress is not null)
        {
            image.SetProgress(progress);
        }
    }

    private void WriteUpright(Image image, Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress)
    {
        var orientation = image.Contains("orientation") ? (int)image.Get("orientation") : 1;
        if (orientation <= 1)
        {
            Encode(image, destination, options);
            return;
        }

        // Girar exige acesso aleatório aos pixels. O limite de pixels já foi conferido.
        source.Seek(0, SeekOrigin.Begin);
        using var loaded = Image.NewFromStream(source, access: Enums.Access.Random, failOn: Enums.FailOn.Error);
        Track(loaded, progress);
        using var rotated = loaded.Autorot();
        Encode(rotated, destination, options);
    }
}
