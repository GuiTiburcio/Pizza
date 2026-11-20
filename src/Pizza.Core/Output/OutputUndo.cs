namespace Pizza.Core.Output;

/// <summary>A Lixeira do Windows, atrás de uma interface pequena para os testes não apagarem nada de verdade.</summary>
public interface IRecycleBin
{
    /// <summary>Envia todos os caminhos de uma vez; falso se algum não foi.</summary>
    bool Recycle(IReadOnlyList<string> paths);
}

/// <summary>Flags do SHFileOperation (FO_DELETE) usadas pelo Desfazer.</summary>
public static class RecycleFlags
{
    public const ushort Silent = 0x4;
    public const ushort NoConfirmation = 0x10;
    public const ushort AllowUndo = 0x40;
    public const ushort NoErrorUi = 0x400;

    /// <summary>Avisa antes de apagar de vez quando a Lixeira não aceita o arquivo; vence o NoConfirmation.</summary>
    public const ushort WantNukeWarning = 0x4000;

    public const ushort Delete = Silent | NoConfirmation | AllowUndo | NoErrorUi | WantNukeWarning;
}

public enum UndoOutcome
{
    Done,

    /// <summary>Algum arquivo mudou, sumiu ou virou outra coisa desde a conversão: nada foi apagado.</summary>
    Changed,

    Failed,
}

/// <summary>
/// Desfazer uma conversão: manda para a Lixeira só os arquivos que esta conversão criou, e só se ainda forem
/// exatamente os mesmos (tamanho e hora de gravação). A lista vem do resultado da conversão; o caminho do arquivo
/// de entrada nunca entra (SEC-05, SEC-31).
/// </summary>
public sealed class OutputUndo
{
    private readonly List<(string Path, long Length, DateTime WrittenUtc)> _files;
    private bool _used;

    private OutputUndo(List<(string Path, long Length, DateTime WrittenUtc)> files) => _files = files;

    /// <summary>
    /// Registra os arquivos recém-criados em <paramref name="directory"/>. Nulo se algum nome não for um nome simples,
    /// coincidir com uma entrada ou não existir: sem garantia, não se oferece desfazer.
    /// </summary>
    public static OutputUndo? Capture(string directory, IReadOnlyList<string> names, IReadOnlyList<string> sources)
    {
        if (names.Count == 0)
        {
            return null;
        }

        var files = new List<(string, long, DateTime)>(names.Count);
        foreach (var name in names)
        {
            if (name.Length == 0 || name != Path.GetFileName(name))
            {
                return null;
            }

            var path = Path.Combine(directory, name);
            if (sources.Any(source => string.Equals(Path.GetFullPath(source), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var info = new FileInfo(path);
            if (!info.Exists || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return null;
            }

            files.Add((path, info.Length, info.LastWriteTimeUtc));
        }

        return new OutputUndo(files);
    }

    /// <summary>Todos os arquivos vão juntos ou nenhum vai. Só funciona uma vez.</summary>
    public UndoOutcome Undo(IRecycleBin bin)
    {
        if (_used)
        {
            return UndoOutcome.Failed;
        }

        foreach (var (path, length, written) in _files)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.Length != length || info.LastWriteTimeUtc != written)
            {
                return UndoOutcome.Changed;
            }
        }

        _used = true;
        return bin.Recycle(_files.Select(file => file.Path).ToArray()) ? UndoOutcome.Done : UndoOutcome.Failed;
    }
}
