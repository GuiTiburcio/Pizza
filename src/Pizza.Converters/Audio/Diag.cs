namespace Pizza.Converters.Audio;

// TEMPORARIO (issue 101): sera removido antes do commit final. So codigos curtos, nunca nome de arquivo (PRIV-16).
internal static class Diag
{
    [ThreadStatic] public static string? Stage;
    [ThreadStatic] public static string? Last;
    [ThreadStatic] public static int Reads;
    [ThreadStatic] public static int Writes;
    [ThreadStatic] public static long Bytes;
    [ThreadStatic] public static uint LastFlags;
    public static volatile string? Callback;

    public static string Take()
    {
        var text = $"{Last ?? "sem-ponto"} stage={Stage} cb={Callback} reads={Reads} writes={Writes} bytes={Bytes} lastflags={LastFlags}";
        Reads = 0; Writes = 0; Bytes = 0; LastFlags = 0;
        Last = null;
        Callback = null;
        return text;
    }
}
