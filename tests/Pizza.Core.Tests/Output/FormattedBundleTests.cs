using System.Text;
using Pizza.Core.Output;

namespace Pizza.Core.Tests.Output;

/// <summary>
/// ADR-038: pacote com um formato por parte, para o DOCX → Markdown com as imagens ao lado. Quem escreve é o
/// Worker, que não é confiável; a lista de formatos permitidos vem da extensão da conversão, no residente.
/// </summary>
public sealed class FormattedBundleTests : IDisposable
{
    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 1, 2];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1];
    private static readonly byte[] Gif = [.. "GIF89a"u8, 1];
    private static readonly byte[] Webp = [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8];
    private static readonly byte[] Markdown = "# Título\n"u8.ToArray();

    private readonly string _file = Path.Combine(Path.GetTempPath(), "pizza-pacote2-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Controle_interrompe_a_validacao_utf8_entre_blocos(bool timeout)
    {
        File.WriteAllBytes(_file, Written((PartFormat.Markdown, Encoding.UTF8.GetBytes(new string('a', 256 * 1024)))));
        using var handle = File.OpenHandle(_file);
        var checks = 0;
        var error = Record.Exception(() => OutputBundle.ReadFormatted(handle, 10, "md", check: () =>
        {
            if (++checks == 4)
            {
                if (timeout) throw new TimeoutException();
                throw new OperationCanceledException();
            }
        }));
        Assert.Equal(4, checks);
        if (timeout) Assert.IsType<TimeoutException>(error);
        else Assert.IsType<OperationCanceledException>(error);
    }

    public void Dispose() => File.Delete(_file);

    private static byte[] Written(params (PartFormat Format, byte[] Content)[] parts)
    {
        using var stream = new MemoryStream();
        var writer = new OutputBundle.Writer(stream, parts.Length, formatted: true);
        foreach (var (format, content) in parts)
        {
            writer.Add(format, content);
        }

        writer.Finish();
        return stream.ToArray();
    }

    private IReadOnlyList<OutputBundle.FormattedPart> Read(byte[] data, string extension = "md", int maxParts = 10)
    {
        File.WriteAllBytes(_file, data);
        using var handle = File.OpenHandle(_file);
        return OutputBundle.ReadFormatted(handle, maxParts, extension);
    }

    [Fact]
    public void Markdown_e_imagens_voltam_com_formato_tamanho_e_ordem()
    {
        var parts = Read(Written((PartFormat.Markdown, Markdown), (PartFormat.Png, Png), (PartFormat.Jpeg, Jpeg), (PartFormat.Gif, Gif), (PartFormat.Webp, Webp)));

        Assert.Equal([PartFormat.Markdown, PartFormat.Png, PartFormat.Jpeg, PartFormat.Gif, PartFormat.Webp], parts.Select(part => part.Format));
        Assert.Equal(Markdown.Length, parts[0].Length);
        var bytes = File.ReadAllBytes(_file);
        Assert.Equal(Jpeg, bytes[(int)parts[2].Offset..(int)(parts[2].Offset + parts[2].Length)]);
    }

    [Fact]
    public void Markdown_sozinho_tambem_vale()
    {
        Assert.Single(Read(Written((PartFormat.Markdown, Markdown))));
    }

    [Fact]
    public void Markdown_precisa_ser_o_primeiro_e_unico()
    {
        Assert.Throws<InvalidDataException>(() => Read(Written((PartFormat.Png, Png), (PartFormat.Markdown, Markdown))));
        Assert.Throws<InvalidDataException>(() => Read(Written((PartFormat.Markdown, Markdown), (PartFormat.Markdown, Markdown))));
        Assert.Throws<InvalidDataException>(() => Read(Written((PartFormat.Png, Png))));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)6)]
    [InlineData((byte)255)]
    public void Formato_fora_da_lista_e_recusado(byte format)
    {
        Assert.Throws<InvalidDataException>(() => Read(Written((PartFormat.Markdown, Markdown), ((PartFormat)format, Png))));
    }

    [Fact]
    public void Imagem_que_nao_comeca_pela_assinatura_do_formato_e_recusada()
    {
        // Um "PNG" que é um executável não vira arquivo .png.
        Assert.Throws<InvalidDataException>(() => Read(Written((PartFormat.Markdown, Markdown), (PartFormat.Png, "MZ executavel"u8.ToArray()))));
        Assert.Throws<InvalidDataException>(() => Read(Written((PartFormat.Markdown, Markdown), (PartFormat.Jpeg, Png))));
    }

    [Fact]
    public void Markdown_que_nao_e_utf8_valido_e_recusado()
    {
        Assert.Throws<InvalidDataException>(() => Read(Written((PartFormat.Markdown, [(byte)'a', 0xFF, 0xFE]))));
    }

    [Fact]
    public void Pacote_antigo_ou_extensao_sem_lista_e_recusado()
    {
        // O PZB1 do Dividir não serve para o Markdown, e o PZB2 não serve para quem não tem lista de formatos.
        using (var stream = new MemoryStream())
        {
            var writer = new OutputBundle.Writer(stream, 1);
            writer.Add(Markdown);
            writer.Finish();
            Assert.Throws<InvalidDataException>(() => Read(stream.ToArray()));
        }

        Assert.Throws<InvalidDataException>(() => Read(Written((PartFormat.Markdown, Markdown)), extension: "pdf"));
        File.WriteAllBytes(_file, Written((PartFormat.Markdown, Markdown)));
        using var handle = File.OpenHandle(_file);
        Assert.Throws<InvalidDataException>(() => OutputBundle.Read(handle, 10, "pdf"));
    }

    [Fact]
    public void Cortado_sobrando_ou_com_contagem_fora_do_limite_e_recusado()
    {
        var good = Written((PartFormat.Markdown, Markdown), (PartFormat.Png, Png));

        Assert.Throws<InvalidDataException>(() => Read(good[..^1]));
        Assert.Throws<InvalidDataException>(() => Read([.. good, 0]));
        Assert.Throws<InvalidDataException>(() => Read(good, maxParts: 1));
    }

    [Fact]
    public void Escritor_comum_nao_aceita_formato_e_vice_versa()
    {
        var plain = new OutputBundle.Writer(new MemoryStream(), 1);
        Assert.Throws<InvalidOperationException>(() => plain.Add(PartFormat.Png, Png));

        var formatted = new OutputBundle.Writer(new MemoryStream(), 1, formatted: true);
        Assert.Throws<InvalidOperationException>(() => formatted.Add(Png));
    }

    [Fact]
    public void Parte_com_formato_escrita_em_fluxo()
    {
        using var stream = new MemoryStream();
        var writer = new OutputBundle.Writer(stream, 1, formatted: true);
        writer.Add(PartFormat.Markdown, () => stream.Write(Encoding.UTF8.GetBytes("texto")));
        writer.Finish();

        Assert.Equal(5, Read(stream.ToArray())[0].Length);
    }

    [Theory]
    [InlineData(PartFormat.Png, "png")]
    [InlineData(PartFormat.Jpeg, "jpg")]
    [InlineData(PartFormat.Gif, "gif")]
    [InlineData(PartFormat.Webp, "webp")]
    [InlineData(PartFormat.Markdown, "md")]
    public void Cada_formato_tem_a_sua_extensao(PartFormat format, string extension)
    {
        Assert.Equal(extension, OutputBundle.Extension(format));
    }
}
