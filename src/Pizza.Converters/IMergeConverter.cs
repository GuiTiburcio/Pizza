namespace Pizza.Converters;

/// <summary>Conversão com várias entradas, na ordem do arraste (Juntar, ADR-033). Também roda só no Worker.</summary>
public interface IMergeConverter : IConverter
{
    string? Convert(IReadOnlyList<Stream> sources, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null);
}
