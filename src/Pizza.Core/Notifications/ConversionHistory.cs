using Pizza.Core.Conversions;

namespace Pizza.Core.Notifications;

public enum HistoryState
{
    Running,
    Done,
    Failed,
}

/// <summary>Uma conversão desta sessão, com o necessário para abrir ou repetir.</summary>
public sealed class HistoryEntry(long id, string sourcePath, ConverterInfo converter, IReadOnlyDictionary<string, string> options)
{
    public long Id { get; } = id;

    public string SourcePath { get; } = sourcePath;

    public ConverterInfo Converter { get; } = converter;

    public IReadOnlyDictionary<string, string> Options { get; } = options;

    public HistoryState State { get; internal set; }

    public string? OutputPath { get; internal set; }

    public string? Message { get; internal set; }

    /// <summary>O arquivo gerado foi movido ou apagado; descoberto só na hora do clique.</summary>
    public bool Missing { get; set; }

    public string Name => Path.GetFileName(OutputPath ?? SourcePath);
}

/// <summary>Conversões desde que o Pizza abriu (FR-44). Só em memória: nada disto vai para disco ou log (ADR-018).</summary>
public sealed class ConversionHistory
{
    private const int MaxEntries = 100;

    private readonly List<HistoryEntry> _entries = [];

    /// <summary>Diz se uma conversão ainda tem cartão na tela: sem o registro, os botões dele parariam.</summary>
    public Func<long, bool> InUse { get; init; } = _ => false;

    /// <summary>As mais novas primeiro.</summary>
    public IReadOnlyList<HistoryEntry> Items => _entries;

    public void Add(long id, string sourcePath, ConverterInfo converter, IReadOnlyDictionary<string, string> options)
    {
        _entries.Insert(0, new HistoryEntry(id, sourcePath, converter, options));
        Trim();
    }

    public HistoryEntry? Find(long id) => _entries.Find(entry => entry.Id == id);

    public void Complete(long id, string outputPath)
    {
        if (Find(id) is { } entry)
        {
            entry.State = HistoryState.Done;
            entry.OutputPath = outputPath;
        }
    }

    public void Fail(long id, string message)
    {
        if (Find(id) is { } entry)
        {
            entry.State = HistoryState.Failed;
            entry.Message = message;
        }
    }

    public void Remove(long id) => _entries.RemoveAll(entry => entry.Id == id);

    /// <summary>Tira as mais antigas, poupando o que ainda está em andamento ou com cartão na tela.</summary>
    private void Trim()
    {
        for (var index = _entries.Count - 1; index >= 0 && _entries.Count > MaxEntries; index--)
        {
            if (_entries[index].State != HistoryState.Running && !InUse(_entries[index].Id))
            {
                _entries.RemoveAt(index);
            }
        }
    }
}
