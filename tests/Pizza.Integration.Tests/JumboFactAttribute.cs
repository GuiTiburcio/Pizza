namespace Pizza.Integration.Tests;

/// <summary>
/// Teste do motor de documentos do Pizza Jumbo (ADR-034). Precisa do motor extraído, apontado por PIZZA_MOTOR
/// (a pasta que contém "program"). Sem ele, o teste aparece como ignorado, com o motivo, nunca como aprovado.
/// </summary>
public sealed class JumboFactAttribute : FactAttribute
{
    public JumboFactAttribute()
    {
        if (Engine is null)
        {
            Skip = "Motor de documentos ausente: defina PIZZA_MOTOR com a pasta do LibreOffice extraído (ADR-034).";
        }
    }

    public static string? Engine =>
        Environment.GetEnvironmentVariable("PIZZA_MOTOR") is { } root && File.Exists(Path.Combine(root, "program", "mergedlo.dll"))
            ? root
            : null;
}
