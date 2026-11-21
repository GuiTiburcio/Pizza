namespace Pizza.Converters.Audio;

// TEMPORARIO (issue 101): sera removido antes do commit final. So codigos curtos, nunca nome de arquivo (PRIV-16).
internal static class Diag
{
    [ThreadStatic] public static string? Stage;
    [ThreadStatic] public static string? Last;
    public static volatile string? Callback;

    public static string Take()
    {
        var text = $"{Last ?? "sem-ponto"} stage={Stage} cb={Callback}";
        Last = null;
        Callback = null;
        return text;
    }
}
