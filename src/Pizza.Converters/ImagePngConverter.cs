using System.Globalization;
using NetVips;
using Image = NetVips.Image;

namespace Pizza.Converters;

/// <summary>JPG → PNG. Roda só dentro do Worker isolado: o arquivo de entrada não é confiável.</summary>
public sealed class ImagePngConverter : IConverter
{
    public const string ConverterId = "image.png";

    private const long MaxSourceBytes = 2L * 1024 * 1024 * 1024; // SEC-40
    private const long MaxPixels = 100_000_000; // SEC-41

    public string Id => ConverterId;

    public string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options)
    {
        if (source.Length > MaxSourceBytes)
        {
            return "LIMIT_EXCEEDED";
        }

        // SEC-45: bloqueia todos os decodificadores e libera só o que esta conversão aceita.
        global::NetVips.NetVips.BlockUntrusted = true;
        Operation.Block("VipsForeignLoad", true);
        Operation.Block("VipsForeignLoadJpeg", false);

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

            WritePng(header, source, destination, options);
            return null;
        }
        catch (VipsException)
        {
            return "CORRUPTED";
        }
    }

    private static void WritePng(Image image, Stream source, Stream destination, IReadOnlyDictionary<string, string> options)
    {
        var orientation = image.Contains("orientation") ? (int)image.Get("orientation") : 1;
        if (orientation <= 1)
        {
            Save(image, destination, options);
            return;
        }

        // Girar exige acesso aleatório aos pixels. O limite de pixels já foi conferido.
        source.Seek(0, SeekOrigin.Begin);
        using var loaded = Image.NewFromStream(source, access: Enums.Access.Random, failOn: Enums.FailOn.Error);
        using var rotated = loaded.Autorot();
        Save(rotated, destination, options);
    }

    private static void Save(Image image, Stream destination, IReadOnlyDictionary<string, string> options)
    {
        // Sem perda em qualquer nível. Em 12 MP, o nível 2 gastou 18% menos tempo que o 6, com arquivo 3% maior.
        var compression = options["compression"] switch
        {
            "fast" => 1,
            "maximum" => 9,
            _ => 2,
        };

        if (options["resolution"] == "original")
        {
            Write(image, destination, compression);
            return;
        }

        using var resized = image.Resize(int.Parse(options["resolution"], CultureInfo.InvariantCulture) / 100.0);
        Write(resized, destination, compression);
    }

    // PRIV-30 e PRIV-31: mantém só o perfil de cor; GPS, autor e afins ficam para trás.
    private static void Write(Image image, Stream destination, int compression) =>
        image.PngsaveStream(destination, compression: compression, keep: Enums.ForeignKeep.Icc);
}
