using System.Collections.ObjectModel;

namespace Pizza.Core.Conversions;

/// <summary>Escolha explícita de destino e opções para um grupo; ainda não inicia conversão.</summary>
public sealed record BatchChoice(ConverterInfo Converter, IReadOnlyDictionary<string, string> Options);

/// <summary>Arquivos com as mesmas saídas possíveis, na ordem em que foram selecionados.</summary>
public sealed class BatchGroup
{
    internal BatchGroup(string[] paths, ConverterInfo[] converters)
    {
        Paths = Array.AsReadOnly(paths);
        Converters = Array.AsReadOnly(converters);
    }

    public IReadOnlyList<string> Paths { get; }
    public IReadOnlyList<ConverterInfo> Converters { get; }
    public int Tab => Converters[0].Tab;
    public BatchChoice? Choice { get; internal set; }
}

/// <summary>
/// Plano em memória do lote misto (FR-31 / TST-01). Não abre arquivos, executa Workers nem publica saídas.
/// A confirmação exige uma escolha por grupo e nenhuma entrada sem suporte. Juntar PDFs é uma operação
/// separada: este plano representa conversões individuais, inclusive quando uma delas divide um arquivo.
/// </summary>
public sealed class BatchPlan
{
    public BatchPlan(IReadOnlyList<string> paths)
    {
        if (paths.Count is < 1 or > ConverterInfo.MaxFiles)
        {
            throw new ArgumentOutOfRangeException(nameof(paths));
        }

        if (paths.Any(string.IsNullOrWhiteSpace) || paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Count)
        {
            throw new ArgumentException("O lote precisa de caminhos não vazios e sem repetição.", nameof(paths));
        }

        Paths = Array.AsReadOnly(paths.ToArray());
        var unsupported = new List<string>();
        var groups = new List<(List<string> Paths, ConverterInfo[] Converters)>();
        // Fotografar a disponibilidade do motor mantém o plano coerente durante sua construção.
        var documentEngineAvailable = ConverterCatalog.DocumentEngineAvailable;
        var installed = ConverterCatalog.All.Where(converter => !converter.Merges
            && (!converter.DocumentEngine || documentEngineAvailable)).ToArray();
        foreach (var path in Paths)
        {
            var fitting = installed.Where(converter => converter.Accepts([path])).ToArray();
            if (fitting.Length == 0)
            {
                unsupported.Add(path);
                continue;
            }

            var tab = fitting.Min(converter => converter.Tab);
            fitting = fitting.Where(converter => converter.Tab == tab).ToArray();
            var index = groups.FindIndex(group => group.Converters.Select(converter => converter.Id)
                .SequenceEqual(fitting.Select(converter => converter.Id)));
            if (index < 0)
            {
                groups.Add((new List<string> { path }, fitting));
            }
            else
            {
                groups[index].Paths.Add(path);
            }
        }

        Groups = Array.AsReadOnly(groups.Select(group => new BatchGroup(group.Paths.ToArray(), group.Converters)).ToArray());
        Unsupported = Array.AsReadOnly(unsupported.ToArray());
    }

    public IReadOnlyList<string> Paths { get; }
    public IReadOnlyList<BatchGroup> Groups { get; }
    public IReadOnlyList<string> Unsupported { get; }
    public bool IsReady => Unsupported.Count == 0 && Groups.Count > 0 && Groups.All(group => group.Choice is not null);

    /// <summary>Recusa destinos de outro grupo e opções fora do catálogo, preservando a escolha anterior.</summary>
    public bool TryChoose(int groupIndex, string converterId, IReadOnlyDictionary<string, string> options)
    {
        if ((uint)groupIndex >= (uint)Groups.Count)
        {
            return false;
        }

        var group = Groups[groupIndex];
        var converter = group.Converters.FirstOrDefault(candidate => candidate.Id == converterId);
        if (converter is null || ConverterCatalog.ResolveOptions(converter, options) is not { } resolved)
        {
            return false;
        }

        group.Choice = new BatchChoice(converter, new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(resolved)));
        return true;
    }

    public void ClearChoice(int groupIndex) => Groups[groupIndex].Choice = null;
}
