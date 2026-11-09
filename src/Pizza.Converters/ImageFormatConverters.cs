using System.Buffers.Binary;
using NetVips;
using Image = NetVips.Image;

namespace Pizza.Converters;

public sealed class ImageJpegConverter : ImageConverter
{
    public override string Id => "image.jpg";

    protected override void Encode(Image image, Stream destination, IReadOnlyDictionary<string, string> options)
    {
        var quality = Number(options, "quality");
        if (!image.HasAlpha())
        {
            image.JpegsaveStream(destination, q: quality, keep: Enums.ForeignKeep.Icc);
            return;
        }

        // JPG não tem transparência: sem isto, as áreas transparentes sairiam pretas.
        // Em 8 bits primeiro, porque em imagens de 16 bits o branco não é 255.
        using var srgb = image.Colourspace(Enums.Interpretation.Srgb);
        using var flattened = srgb.Flatten(background: [255.0]);
        flattened.JpegsaveStream(destination, q: quality, keep: Enums.ForeignKeep.Icc);
    }
}

public sealed class ImageWebpConverter : ImageConverter
{
    public override string Id => "image.webp";

    protected override void Encode(Image image, Stream destination, IReadOnlyDictionary<string, string> options) =>
        image.WebpsaveStream(destination, q: Number(options, "quality"), keep: Enums.ForeignKeep.Icc);
}

public sealed class ImageTiffConverter : ImageConverter
{
    public override string Id => "image.tiff";

    protected override void Encode(Image image, Stream destination, IReadOnlyDictionary<string, string> options) =>
        image.TiffsaveStream(destination, compression: Enums.ForeignTiffCompression.Deflate, keep: Enums.ForeignKeep.Icc);
}

public sealed class ImageGifConverter : ImageConverter
{
    public override string Id => "image.gif";

    // Em 16 MP, o esforço 1 gastou cerca de 40% menos tempo que o padrão 7, com tamanho quase igual.
    protected override void Encode(Image image, Stream destination, IReadOnlyDictionary<string, string> options)
    {
        using var srgb = ToSrgb(image);
        srgb.GifsaveStream(destination, effort: 1, keep: Enums.ForeignKeep.None);
    }
}

/// <summary>ICO com uma única imagem PNG dentro; o libvips não grava este formato.</summary>
public sealed class ImageIcoConverter : ImageConverter
{
    private const int HeaderBytes = 22;

    public override string Id => "image.ico";

    protected override void Encode(Image image, Stream destination, IReadOnlyDictionary<string, string> options)
    {
        var size = Number(options, "size");
        using var converted = ToSrgb(image);
        using var srgb = converted.ThumbnailImage(size, height: size);
        using var withAlpha = srgb.HasAlpha() ? srgb.Copy() : srgb.Bandjoin(255.0);

        // Imagem que não é quadrada ganha margens transparentes em vez de ser esticada.
        using var square = withAlpha.Gravity(Enums.CompassDirection.Centre, size, size, extend: Enums.Extend.Background, background: [0.0, 0.0, 0.0, 0.0]);
        var png = square.PngsaveBuffer(keep: Enums.ForeignKeep.None);

        Span<byte> header = stackalloc byte[HeaderBytes];
        header.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(header[2..], 1); // Tipo: ícone.
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..], 1); // Uma imagem.
        header[6] = (byte)(size % 256); // O formato usa 0 para 256 px.
        header[7] = (byte)(size % 256);
        BinaryPrimitives.WriteUInt16LittleEndian(header[10..], 1); // Planos.
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], 32); // Bits por pixel.
        BinaryPrimitives.WriteInt32LittleEndian(header[14..], png.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header[18..], HeaderBytes);

        destination.Write(header);
        destination.Write(png);
    }
}
