using Pizza.Core.Output;

namespace Pizza.Core.Tests.Output;

public class ZoneIdentifierTests
{
    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("utf-32")]
    public void Leitura_limitada_preserva_os_boms(string name)
    {
        var encoding = System.Text.Encoding.GetEncoding(name);
        using var source = new MemoryStream([.. encoding.GetPreamble(), .. encoding.GetBytes("[ZoneTransfer]\r\nZoneId=3\r\n")]);
        Assert.Equal(3, ZoneIdentifier.Read(source));
    }

    [Fact]
    public void Zona_no_inicio_nao_esconde_cauda_acima_do_teto()
    {
        var data = new byte[ZoneIdentifier.MaxBytes + 1000];
        "ZoneId=3\n"u8.CopyTo(data);
        using var source = new MemoryStream(data);
        Assert.Throws<ConversionLimitException>(() => ZoneIdentifier.Read(source));
        Assert.Equal(ZoneIdentifier.MaxBytes + 1, source.Position);
    }

    [Fact]
    public void Aceita_o_teto_exato_sem_confiar_em_length()
    {
        using var source = new ChunkedZoneStream(ZoneIdentifier.MaxBytes);
        Assert.Equal(3, ZoneIdentifier.Read(source));
    }

    [Fact]
    public void Stream_que_entrega_mais_bytes_durante_a_leitura_e_limitado()
    {
        using var source = new ChunkedZoneStream(ZoneIdentifier.MaxBytes + 1000);
        Assert.Throws<ConversionLimitException>(() => ZoneIdentifier.Read(source));
        Assert.Equal(ZoneIdentifier.MaxBytes + 1, source.BytesRead);
    }

    private sealed class ChunkedZoneStream(int size) : Stream
    {
        public int BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            var count = Math.Min(17, Math.Min(buffer.Length, size - BytesRead));
            for (var index = 0; index < count; index++)
            {
                var position = BytesRead + index;
                buffer[index] = position < 9 ? "ZoneId=3\n"u8[position] : (byte)' ';
            }

            BytesRead += count;
            return count;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public void Le_a_zona_e_ignora_as_urls()
    {
        Assert.Equal(3, ZoneIdentifier.ParseZone("[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://site.example/a.jpg\r\n"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[ZoneTransfer]\r\n")]
    [InlineData("[ZoneTransfer]\r\nZoneId=abc\r\n")]
    [InlineData("[ZoneTransfer]\r\nZoneId=9\r\n")]
    [InlineData("[ZoneTransfer]\r\nZoneId=-1\r\n")]
    public void Conteudo_invalido_nao_tem_zona(string content)
    {
        Assert.Null(ZoneIdentifier.ParseZone(content));
    }

    [Theory]
    [InlineData(new[] { 1, 3 }, 3)]
    [InlineData(new[] { 3, 1 }, 3)]
    [InlineData(new[] { 0, 2, 4 }, 4)]
    public void Entre_varios_arquivos_vale_a_zona_mais_restritiva(int[] zones, int expected)
    {
        // SEC-35 no Juntar: quanto maior o ZoneId, mais restrito (4 é "sites restritos").
        Assert.Equal(expected, ZoneIdentifier.MostRestrictive(zones.Select(zone => (int?)zone)));
    }

    [Fact]
    public void Sem_marca_em_nenhum_arquivo_nao_ha_zona()
    {
        Assert.Null(ZoneIdentifier.MostRestrictive([null, null]));
    }

    [Fact]
    public void Texto_gravado_tem_so_a_zona()
    {
        // SEC-35: HostUrl e ReferrerUrl não são copiados.
        Assert.Equal("[ZoneTransfer]\r\nZoneId=3\r\n", ZoneIdentifier.Format(3));
    }
}
