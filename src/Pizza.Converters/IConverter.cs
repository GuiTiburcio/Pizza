namespace Pizza.Converters;

/// <summary>Uma conversão. Roda só dentro do Worker isolado; cancelar é encerrar o Worker.</summary>
public interface IConverter
{
    string Id { get; }

    /// <summary>Converte e devolve null, ou o código de erro da SPECS §10.4. As opções já vêm validadas.</summary>
    string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options);
}
