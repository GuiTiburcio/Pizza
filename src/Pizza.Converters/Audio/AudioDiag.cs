namespace Pizza.Converters.Audio;

// TEMPORARIO (diagnostico do CI): sem nome de arquivo nem conteudo (PRIV-16).
internal static class AudioDiag
{
    [ThreadStatic] public static string? Last;
    [ThreadStatic] public static string? Wait;
}
