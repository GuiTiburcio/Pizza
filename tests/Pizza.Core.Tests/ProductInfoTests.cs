using Pizza.Core;

namespace Pizza.Core.Tests;

public class ProductInfoTests
{
    [Fact]
    public void DataFolder_fica_em_LocalAppData_com_o_nome_do_produto()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.Equal(Path.Combine(localAppData, "Pizza"), ProductInfo.DataFolder);
    }
}
