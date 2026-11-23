using System.Buffers.Binary;
using NetVips;
using Pizza.Converters.Docs;

namespace Pizza.Converters.Tests;

public sealed class DocxImageMetadataTests
{
    [Fact]
    public void Gif_preserva_texto_quando_ele_e_o_unico_elemento_visivel()
    {
        var gif = Convert.FromHexString("47494638396108000800800000000000FFFFFF21010C0000000008000800080801000141003B");
        Assert.Same(gif, GifMetadata.Remove(gif));
    }

    [Fact]
    public void Exif_mantem_resolucoes_que_compartilham_o_mesmo_valor()
    {
        var tiff = Convert.FromHexString("49492A000800000002001A01050001000000260000001B0105000100000026000000000000006000000001000000");
        Assert.Equal(tiff, TechnicalExif.Keep(tiff));
    }

    [Theory]
    // Orientação 0 (gravada por algumas câmeras) e unidade 2: só a unidade fica.
    [InlineData("49492A0008000000020012010300010000000000000028010300010000000200000000000000", "49492A0008000000010028010300010000000200000000000000")]
    // Orientação repetida: vale a primeira.
    [InlineData("49492A00080000000200120103000100000006000000120103000100000003000000000000000000", "49492A0008000000010012010300010000000600000000000000")]
    // Orientação com contagem 2: descartada.
    [InlineData("49492A000800000001001201030002000000060006000000000000", "")]
    // Cabeçalho que não é TIFF: o EXIF inteiro sai.
    [InlineData("4D4D002B0000000800000000", "")]
    // Diretório fora do bloco.
    [InlineData("49492A00FF000000", "")]
    public void Exif_invalido_descarta_o_campo_ou_o_bloco_sem_erro(string input, string expected)
    {
        Assert.Equal(Convert.FromHexString(expected), TechnicalExif.Keep(Convert.FromHexString(input)));
    }

    [Fact]
    public void Jpeg_com_dois_blocos_exif_fica_so_com_os_campos_tecnicos_do_primeiro()
    {
        using var pixels = Image.Black(8, 8);
        var jpg = pixels.JpegsaveBuffer(keep: Enums.ForeignKeep.None);
        var exif = Convert.FromHexString("FFE1004A4578696600004D4D002A000000080003011200030000000100060000011A00050000000100000032013B0002000000080000003A0000000000000060000000014669637469636100");
        var technical = Convert.FromHexString("FFE1003645786966000049492A000800000002001201030001000000060000001A0105000100000026000000000000006000000001000000");
        var afterJfif = 4 + BinaryPrimitives.ReadUInt16BigEndian(jpg.AsSpan(4));
        byte[] input = [.. jpg[..afterJfif], .. exif, .. exif, .. jpg[afterJfif..]];
        byte[] expected = [.. jpg[..afterJfif], .. technical, .. jpg[afterJfif..]];

        Assert.Equal(expected, JpegMetadata.Remove(input));
    }

    [Fact]
    public void Gif_preserva_repeticao_e_quadros_sem_comentario()
    {
        // GIF 1×1, paleta global e dois frames. O controle gráfico, o loop e os dados LZW devem ficar intactos.
        var header = Convert.FromHexString("47494638396101000100800000000000FFFFFF");
        var loop = Convert.FromHexString("21FF0B4E45545343415045322E300301020000");
        var frame = Convert.FromHexString("21F904000A0000002C0000000001000100000202440100");
        var comment = Convert.FromHexString("21FE054175746F7200");
        byte[] input = [.. header, .. loop, .. frame, .. comment, .. frame, 0x3b];
        byte[] expected = [.. header, .. loop, .. frame, .. frame, 0x3b];

        Assert.Equal(expected, GifMetadata.Remove(input));
        using var decoded = Image.NewFromBuffer(expected, kwargs: new VOption { ["n"] = -1 });
        Assert.Equal(2, decoded.Height);
    }

    [Fact]
    public void Webp_animado_preserva_quadros_e_atrasos_sem_xmp()
    {
        using var pixels = Image.Black(4, 4);
        var simple = pixels.WebpsaveBuffer(lossless: true, keep: Enums.ForeignKeep.None);
        var frame = Frame(simple[12..]);
        var animation = Chunk("ANIM"u8, [0, 0, 0, 0, 2, 0]);
        var xmp = Chunk("XMP "u8, "<author>Conta de teste</author>"u8);
        var tech = Convert.FromHexString("49492A0008000000010012010300010000000600000000000000");
        var exif = Chunk("EXIF"u8, tech);
        var sourceHeader = Chunk("VP8X"u8, [0x0e, 0, 0, 0, 3, 0, 0, 3, 0, 0]);
        var cleanHeader = Chunk("VP8X"u8, [0x0a, 0, 0, 0, 3, 0, 0, 3, 0, 0]);
        var input = Riff([.. sourceHeader, .. animation, .. frame, .. frame, .. exif, .. xmp]);
        var expected = Riff([.. cleanHeader, .. animation, .. frame, .. frame, .. exif]);

        Assert.Equal(expected, WebpMetadata.Remove(input));
        using var decoded = Image.NewFromBuffer(expected, kwargs: new VOption { ["n"] = -1 });
        Assert.Equal(8, decoded.Height);
    }

    [Fact]
    public void Apng_preserva_controle_e_dados_dos_quadros_ao_limpar_texto()
    {
        var png = Convert.FromBase64String(DocxHtmlConverterTests.Png);
        var control = PngChunk("acTL"u8, Convert.FromHexString("0000000200000002"));
        var first = PngChunk("fcTL"u8, Convert.FromHexString("00000000000000010000000100000000000000000001000A0000"));
        var second = PngChunk("fcTL"u8, Convert.FromHexString("00000001000000010000000100000000000000000001000A0000"));
        var pixels = PngChunk("fdAT"u8, [0, 0, 0, 2, .. png.AsSpan(41, 11)]);
        var text = Convert.FromHexString("0000001574455874417574686F72004175746F7220666963746963696F3FA452C9");
        var provenance = PngChunk("caBX"u8, "fixture de proveniencia"u8);
        byte[] input = [.. png[..33], .. control, .. first, .. png[33..^12], .. text, .. second, .. pixels, .. provenance, .. png[^12..]];
        byte[] expected = [.. png[..33], .. control, .. first, .. png[33..^12], .. second, .. pixels, .. png[^12..]];

        Assert.Equal(expected, PngMetadata.Remove(input));
    }

    private static byte[] Frame(byte[] pixels) => Chunk("ANMF"u8, [0, 0, 0, 0, 0, 0, 3, 0, 0, 3, 0, 0, 100, 0, 0, 0, .. pixels]);

    private static byte[] Riff(byte[] chunks)
    {
        byte[] result = [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, .. chunks];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)(result.Length - 8));
        return result;
    }

    private static byte[] Chunk(ReadOnlySpan<byte> type, ReadOnlySpan<byte> payload)
    {
        var result = new byte[8 + payload.Length + (payload.Length & 1)];
        type.CopyTo(result);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)payload.Length);
        payload.CopyTo(result.AsSpan(8));
        return result;
    }

    private static byte[] PngChunk(ReadOnlySpan<byte> type, ReadOnlySpan<byte> payload)
    {
        byte[] result = [0, 0, 0, 0, .. type, .. payload, 0, 0, 0, 0];
        BinaryPrimitives.WriteUInt32BigEndian(result, (uint)payload.Length);
        // CRC dos fixtures pela tabela de polinômios refletidos.
        Span<uint> table = stackalloc uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var entry = index;
            for (var bit = 0; bit < 8; bit++) entry = (entry >> 1) ^ ((entry & 1) == 1 ? 0xedb88320u : 0);
            table[(int)index] = entry;
        }

        var crc = uint.MaxValue;
        foreach (var value in result.AsSpan(4, 4 + payload.Length)) crc = table[(byte)(crc ^ value)] ^ (crc >> 8);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(result.Length - 4), ~crc);
        return result;
    }
}
