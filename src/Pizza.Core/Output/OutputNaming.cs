using System.Security.Cryptography;

namespace Pizza.Core.Output;

/// <summary>Nomes de saída e de temporário (FR-26, FR-27, SEC-30).</summary>
public static class OutputNaming
{
    public const int MaxAttempts = 10_000;

    /// <summary>
    /// Nomes a tentar, em ordem: <c>a.png</c>, <c>a (1).png</c>, <c>a (2).png</c>… Com sufixo (ADR-033):
    /// <c>a - girado.pdf</c>, <c>a - girado (1).pdf</c>…
    /// </summary>
    public static IEnumerable<string> Candidates(string sourcePath, string extension, string suffix = "")
    {
        var name = Path.GetFileNameWithoutExtension(sourcePath) + suffix;
        yield return $"{name}.{extension}";
        for (var number = 1; number < MaxAttempts; number++)
        {
            yield return $"{name} ({number}).{extension}";
        }
    }

    /// <summary>
    /// Sufixo de cada parte do Dividir (prancha 6A-1), com a parte contada a partir de 1: "Cada página" vira
    /// <c> - pág 01</c> (zeros à esquerda para a ordem no Explorer, pelo menos dois dígitos); "Pela metade",
    /// <c> - parte 1</c>. No CSV do Brotinho (ADR-034), cada planilha vira <c> - planilha 1</c>, e com uma
    /// planilha só o nome fica igual ao do original.
    /// </summary>
    public static string PartSuffix(string mode, int part, int count) => mode switch
    {
        "sheets" => count == 1 ? "" : $" - planilha {part}",
        "pages" => $" - pág {part.ToString(new string('0', Math.Max(2, count.ToString(System.Globalization.CultureInfo.InvariantCulture).Length)), System.Globalization.CultureInfo.InvariantCulture)}",
        _ => $" - parte {part}",
    };

    public static string TemporaryName() =>
        $".~pizza-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16))}.tmp";
}
