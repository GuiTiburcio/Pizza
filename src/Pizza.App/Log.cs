using Pizza.Core;

namespace Pizza.App;

/// <summary>Log local em <c>%LOCALAPPDATA%\Pizza\logs</c>. Nunca grava conteúdo de arquivos (Parte 9).</summary>
internal static class Log
{
    private static readonly string Folder = Path.Combine(ProductInfo.DataFolder, "logs");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, "pizza.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Falha ao registrar log nunca pode derrubar o residente.
        }
    }
}
