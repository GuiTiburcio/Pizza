namespace Pizza.Converters;

/// <summary>Os conversores que o Worker sabe executar.</summary>
public static class ConverterRegistry
{
    public static IReadOnlyList<IConverter> All { get; } = [new ImagePngConverter()];

    public static IConverter? Find(string id) => All.FirstOrDefault(converter => converter.Id == id);
}
