using System.Buffers.Binary;
using Pizza.Core.Output;

namespace Pizza.Core.Tests.Output;

/// <summary>
/// Pacote com várias saídas (ADR-033, Dividir). Quem escreve é o Worker, que não é confiável: o residente só
/// aceita o formato exato e nunca interpreta o conteúdo das partes.
/// </summary>
public sealed class OutputBundleTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "pizza-pacote-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => File.Delete(_file);

    private static byte[] Pdf(string text) => System.Text.Encoding.ASCII.GetBytes("%PDF-" + text);

    private IReadOnlyList<OutputBundle.Part> Read(byte[] data, int maxParts = 10, string extension = "pdf")
    {
        File.WriteAllBytes(_file, data);
        using var handle = File.OpenHandle(_file);
        return OutputBundle.Read(handle, maxParts, extension);
    }

    private static byte[] Written(params byte[][] parts)
    {
        using var stream = new MemoryStream();
        var writer = new OutputBundle.Writer(stream, parts.Length);
        foreach (var part in parts)
        {
            writer.Add(part);
        }

        writer.Finish();
        return stream.ToArray();
    }

    [Fact]
    public void Partes_voltam_na_mesma_ordem_e_tamanho()
    {
        var parts = Read(Written(Pdf("um"), Pdf("dois"), Pdf("tres")));

        Assert.Equal([7L, 9L, 9L], parts.Select(part => part.Length));
        var bytes = File.ReadAllBytes(_file);
        Assert.Equal("%PDF-dois", System.Text.Encoding.ASCII.GetString(bytes, (int)parts[1].Offset, (int)parts[1].Length));
    }

    [Fact]
    public void Escritor_nao_aceita_mais_nem_menos_partes_que_o_anunciado()
    {
        using var stream = new MemoryStream();
        var writer = new OutputBundle.Writer(stream, 1);
        writer.Add(Pdf("a"));
        Assert.Throws<InvalidOperationException>(() => writer.Add(Pdf("b")));

        var short_ = new OutputBundle.Writer(new MemoryStream(), 2);
        short_.Add(Pdf("a"));
        Assert.Throws<InvalidOperationException>(short_.Finish);
    }

    [Fact]
    public void Formato_errado_e_recusado()
    {
        var good = Written(Pdf("um"), Pdf("dois"));

        var magic = good.ToArray();
        magic[0] = (byte)'X';
        Assert.Throws<InvalidDataException>(() => Read(magic));

        Assert.Throws<InvalidDataException>(() => Read([.. good, 0])); // sobra no fim
        Assert.Throws<InvalidDataException>(() => Read(good[..^1])); // cortado
        Assert.Throws<InvalidDataException>(() => Read([]));
    }

    [Fact]
    public void Contagem_fora_dos_limites_e_recusada()
    {
        Assert.Throws<InvalidDataException>(() => Read(Written()));
        Assert.Throws<InvalidDataException>(() => Read(Written(Pdf("a"), Pdf("b"), Pdf("c")), maxParts: 2));
    }

    [Fact]
    public void Tamanho_negativo_ou_gigante_e_recusado_sem_alocar()
    {
        var good = Written(Pdf("um"));
        var negative = good.ToArray();
        BinaryPrimitives.WriteInt64LittleEndian(negative.AsSpan(8), -1);
        Assert.Throws<InvalidDataException>(() => Read(negative));

        var huge = good.ToArray();
        BinaryPrimitives.WriteInt64LittleEndian(huge.AsSpan(8), long.MaxValue);
        Assert.Throws<InvalidDataException>(() => Read(huge));
    }

    [Fact]
    public void Parte_que_nao_comeca_como_pdf_e_recusada()
    {
        Assert.Throws<InvalidDataException>(() => Read(Written(Pdf("um"), "MZ executavel"u8.ToArray())));
    }

    [Fact]
    public void Partes_de_csv_comecam_pelo_bom_do_utf8()
    {
        // Brotinho (ADR-034): cada planilha vira um CSV em UTF-8 com BOM; uma planilha vazia é só o BOM.
        byte[] bom = [0xEF, 0xBB, 0xBF];
        Assert.Equal(2, Read(Written([.. bom, .. "a;b\r\n"u8], bom), extension: "csv").Count);

        Assert.Throws<InvalidDataException>(() => Read(Written("a;b\r\n"u8.ToArray()), extension: "csv"));
        Assert.Throws<InvalidDataException>(() => Read(Written(Pdf("um")), extension: "csv"));
        Assert.Throws<InvalidDataException>(() => Read(Written([.. bom, .. "x"u8]), extension: "pdf"));
    }

    [Fact]
    public void Partes_de_imagem_comecam_pela_assinatura_do_formato()
    {
        // Imagens do PDF (adendo da ADR-033): cada página é um JPG ou um PNG, conferido pelos primeiros bytes.
        byte[] jpg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00];
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
        Assert.Equal(2, Read(Written(jpg, jpg), extension: "jpg").Count);
        Assert.Single(Read(Written(png), extension: "png"));

        Assert.Throws<InvalidDataException>(() => Read(Written(png), extension: "jpg"));
        Assert.Throws<InvalidDataException>(() => Read(Written(jpg), extension: "png"));
        Assert.Throws<InvalidDataException>(() => Read(Written(Pdf("um")), extension: "jpg"));
    }

    [Fact]
    public void Extensao_sem_pacote_conhecido_e_recusada()
    {
        Assert.Throws<InvalidDataException>(() => Read(Written(Pdf("um")), extension: "exe"));
    }

    [Fact]
    public void Teto_soma_as_partes_sem_cobrar_o_envelope()
    {
        File.WriteAllBytes(_file, Written(Pdf("um"), Pdf("dois")));
        using var handle = File.OpenHandle(_file);

        Assert.Equal(2, OutputBundle.Read(handle, 10, "pdf", maxOutputBytes: 16).Count);
        Assert.Throws<ConversionLimitException>(() => OutputBundle.Read(handle, 10, "pdf", maxOutputBytes: 15));
    }

    [Fact]
    public void Comprimento_logico_real_acima_do_teto_e_recusado()
    {
        // O arquivo realmente tem esse comprimento: não é só um número inválido no cabeçalho.
        File.WriteAllBytes(_file, Written(Pdf("um")));
        using (var file = File.OpenWrite(_file))
        {
            file.SetLength(1024);
        }

        using var handle = File.OpenHandle(_file);
        Assert.Throws<ConversionLimitException>(() => OutputBundle.Read(handle, 10, "pdf", maxOutputBytes: 16));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Copia_para_entre_blocos_quando_cancelada_ou_fora_do_prazo(bool timeout)
    {
        File.WriteAllBytes(_file, new byte[128]);
        var target = _file + ".copy";
        try
        {
            using var source = File.OpenHandle(_file);
            using var destination = File.OpenHandle(target, FileMode.CreateNew, FileAccess.Write);
            using var cancellation = new CancellationTokenSource();
            var checks = 0;
            void Check()
            {
                if (++checks == 2)
                {
                    if (timeout)
                    {
                        throw new TimeoutException();
                    }

                    cancellation.Cancel();
                }

                cancellation.Token.ThrowIfCancellationRequested();
            }

            var exception = Record.Exception(() => OutputBundle.CopyPart(source, new OutputBundle.Part(0, 128), destination, new byte[16], Check));
            if (timeout)
            {
                Assert.IsType<TimeoutException>(exception);
            }
            else
            {
                Assert.IsType<OperationCanceledException>(exception);
            }

            Assert.Equal(16, RandomAccess.GetLength(destination));
        }
        finally
        {
            File.Delete(target);
        }
    }
}
