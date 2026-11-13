namespace Pizza.Core.Gesture;

/// <summary>
/// Faixa do Juntar (prancha 6A-1): mostra, antes de soltar, os arquivos numerados na ordem em que vão ficar.
/// Cabem três; com mais, os dois primeiros e quantos faltam.
/// </summary>
public static class MergeOrder
{
    public const int MaxNameLength = 16;
    private const int Visible = 3;

    public static IReadOnlyList<(string Number, string Name)> Chips(IReadOnlyList<string> paths)
    {
        if (paths.Count <= Visible)
        {
            return paths.Select((path, index) => ((index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Short(path))).ToList();
        }

        var rest = paths.Count - (Visible - 1);
        return
        [
            ("1", Short(paths[0])),
            ("2", Short(paths[1])),
            ($"+{rest}", $"mais {rest}"),
        ];
    }

    private static string Short(string path)
    {
        var name = Path.GetFileName(path);
        if (name.Length <= MaxNameLength)
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        return string.Concat(name.AsSpan(0, MaxNameLength - extension.Length - 1), "…", extension);
    }
}
