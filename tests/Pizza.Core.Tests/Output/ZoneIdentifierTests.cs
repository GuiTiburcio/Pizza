using Pizza.Core.Output;

namespace Pizza.Core.Tests.Output;

public class ZoneIdentifierTests
{
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
