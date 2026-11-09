using Pizza.Core;
using Pizza.Core.Diagnostics;

namespace Pizza.App;

/// <summary>Log local em <c>%LOCALAPPDATA%\Pizza\logs</c>: um arquivo por dia, por 30 dias (PRIV-14, PRIV-16).</summary>
internal static class Log
{
    private static DateOnly _lastPurge;

    public static string Folder { get; } = Path.Combine(ProductInfo.DataFolder, "logs");

    public static void Write(LogEvent logEvent)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var now = DateTimeOffset.Now;
            var today = DateOnly.FromDateTime(now.DateTime);
            if (today != _lastPurge)
            {
                Purge(today);
                _lastPurge = today;
            }

            File.AppendAllText(Path.Combine(Folder, LogFormat.FileName(today)), LogFormat.Line(now, logEvent) + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Falha ao registrar log nunca pode derrubar o residente.
        }
    }

    private static void Purge(DateOnly today)
    {
        foreach (var file in Directory.EnumerateFiles(Folder, "pizza-*.log"))
        {
            if (LogFormat.IsExpired(Path.GetFileName(file), today))
            {
                File.Delete(file);
            }
        }
    }
}
