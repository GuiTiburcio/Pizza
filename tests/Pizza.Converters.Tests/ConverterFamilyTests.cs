using Pizza.Converters;

namespace Pizza.Converters.Tests;

public class ConverterFamilyTests
{
    [Fact]
    public void Existem_quatro_familias_uma_por_aba_do_menu()
    {
        Assert.Equal(
            [ConverterFamily.Image, ConverterFamily.Pdf, ConverterFamily.Document, ConverterFamily.Media],
            Enum.GetValues<ConverterFamily>());
    }
}
