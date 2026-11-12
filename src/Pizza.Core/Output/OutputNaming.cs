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

    public static string TemporaryName() =>
        $".~pizza-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16))}.tmp";
}
