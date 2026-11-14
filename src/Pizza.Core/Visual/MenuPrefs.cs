using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Pizza.Core.Visual;

/// <summary>
/// ADR-037 e PRIV-15: o fator de tamanho e a posição dos painéis, em fração da área útil. Só chaves fixas e
/// números; nada de nome de arquivo, conteúdo ou coordenada absoluta. Linha estranha é ignorada.
/// </summary>
public sealed partial class MenuPrefs
{
    public double Scale { get; set; } = MenuScale.Max;

    public Dictionary<string, (double X, double Y)> Positions { get; } = new(StringComparer.Ordinal);

    public static MenuPrefs Parse(string text)
    {
        var prefs = new MenuPrefs();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var equals = line.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var key = line[..equals];
            var value = line[(equals + 1)..];
            if (key == "scale" && Number(value) is { } scale)
            {
                prefs.Scale = MenuScale.Clamp(scale);
            }
            else if (key.StartsWith("position.", StringComparison.Ordinal) && Key(key["position.".Length..]) is { } name)
            {
                var parts = value.Split(';');
                if (parts.Length == 2 && Number(parts[0]) is { } x && Number(parts[1]) is { } y)
                {
                    prefs.Positions[name] = (Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1));
                }
            }
        }

        return prefs;
    }

    public string Serialize()
    {
        var text = new StringBuilder();
        text.Append("scale=").Append(Format(MenuScale.Clamp(Scale))).Append('\n');
        foreach (var (name, (x, y)) in Positions.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (Key(name) is not null)
            {
                text.Append("position.").Append(name).Append('=').Append(Format(Math.Clamp(x, 0, 1))).Append(';').Append(Format(Math.Clamp(y, 0, 1))).Append('\n');
            }
        }

        return text.ToString();
    }

    private static double? Number(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? number : null;

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string? Key(string name) => KeyPattern().IsMatch(name) ? name : null;

    [GeneratedRegex("^[a-z]{1,16}$")]
    private static partial Regex KeyPattern();
}

/// <summary>Posição guardada como fração do espaço livre da área útil, para caber em outra resolução.</summary>
public static class MenuPlacement
{
    public static (double X, double Y) ToFraction(int left, int top, int width, int height, int workLeft, int workTop, int workWidth, int workHeight) =>
        (Fraction(left - workLeft, workWidth - width), Fraction(top - workTop, workHeight - height));

    public static (int Left, int Top) FromFraction(double x, double y, int width, int height, int workLeft, int workTop, int workWidth, int workHeight) =>
        (workLeft + (int)Math.Round(Math.Clamp(x, 0, 1) * Math.Max(0, workWidth - width)),
         workTop + (int)Math.Round(Math.Clamp(y, 0, 1) * Math.Max(0, workHeight - height)));

    private static double Fraction(int offset, int free) => free <= 0 ? 0 : Math.Clamp(offset / (double)free, 0, 1);
}
