using NetVips;
using Image = NetVips.Image;

namespace Pizza.Converters;

/// <summary>JPG → PNG. Roda só dentro do Worker isolado: o arquivo de entrada não é confiável.</summary>
public static class ImagePngConverter
{
    public const string Id = "image.png";

    private const long MaxSourceBytes = 2L * 1024 * 1024 * 1024; // SEC-40
    private const long MaxPixels = 100_000_000; // SEC-41

    // Sem perda em qualquer nível. Em 12 MP, o nível 2 gastou 18% menos tempo que o padrão 6, com arquivo 3% maior.
    private const int PngCompression = 2;

    /// <summary>Converte e devolve null, ou o código de erro da SPECS §10.4.</summary>
    public static string? Convert(Stream source, Stream destination)
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

            WritePng(header, source, destination);
            return null;
        }
        catch (VipsException)
        {
            return "CORRUPTED";
        }
    }

    private static void WritePng(Image image, Stream source, Stream destination)
    {
        var orientation = image.Contains("orientation") ? (int)image.Get("orientation") : 1;
        if (orientation <= 1)
        {
            Save(image, destination);
            return;
        }

        // Girar exige acesso aleatório aos pixels. O limite de pixels já foi conferido.
        source.Seek(0, SeekOrigin.Begin);
        using var loaded = Image.NewFromStream(source, access: Enums.Access.Random, failOn: Enums.FailOn.Error);
        using var rotated = loaded.Autorot();
        Save(rotated, destination);
    }

    // PRIV-30 e PRIV-31: mantém só o perfil de cor; GPS, autor e afins ficam para trás.
    private static void Save(Image image, Stream destination) =>
        image.PngsaveStream(destination, compression: PngCompression, keep: Enums.ForeignKeep.Icc);
}
