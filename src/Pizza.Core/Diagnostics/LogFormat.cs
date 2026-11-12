using System.Globalization;

namespace Pizza.Core.Diagnostics;

/// <summary>
/// Tudo o que o residente registra. Ser um enum é a garantia do PRIV-16: não existe como
/// passar caminho, nome de arquivo ou coordenada para o log.
/// </summary>
public enum LogEvent
{
    AppStarted,
    AppStopped,
    MenuOpened,
    MenuCancelled,
    NotAFile,
    FileDropped,
    ConversionDone,
    ConversionFailed,
    ConversionCancelled,
}

/// <summary>Formato e retenção do log local (PRIV-10, PRIV-14).</summary>
public static class LogFormat
{
    private const int RetentionDays = 30;
    private const string FileDateFormat = "yyyyMMdd";
    private const string FilePrefix = "pizza-";
    private const string FileSuffix = ".log";

    public static string Name(LogEvent logEvent) => logEvent switch
    {
        LogEvent.AppStarted => "app.started",
        LogEvent.AppStopped => "app.stopped",
        LogEvent.MenuOpened => "menu.opened",
        LogEvent.MenuCancelled => "menu.cancelled",
        LogEvent.NotAFile => "menu.notfile",
        LogEvent.FileDropped => "menu.dropped",
        LogEvent.ConversionDone => "conversion.done",
        LogEvent.ConversionFailed => "conversion.failed",
        LogEvent.ConversionCancelled => "conversion.cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(logEvent)),
    };

    private const int MaxMilliseconds = 99999;

    /// <summary>
    /// <paramref name="milliseconds"/>: só no menu aberto, o tempo do gesto até o menu (NFR-03). É um número, então
    /// não tem como carregar texto (PRIV-16).
    /// </summary>
    public static string Line(DateTimeOffset moment, LogEvent logEvent, int? milliseconds = null)
    {
        if (milliseconds is not null && logEvent != LogEvent.MenuOpened)
        {
            throw new ArgumentException("Só o menu aberto leva tempo.", nameof(milliseconds));
        }

        var timestamp = moment.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
        var time = milliseconds is { } ms ? $",\"ms\":{Math.Clamp(ms, 0, MaxMilliseconds).ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        return $$"""{"ts":"{{timestamp}}","lvl":"info","evt":"{{Name(logEvent)}}"{{time}}}""";
    }

    public static string FileName(DateOnly day) =>
        FilePrefix + day.ToString(FileDateFormat, CultureInfo.InvariantCulture) + FileSuffix;

    /// <summary>Verdadeiro só para um arquivo de log nosso com mais de 30 dias.</summary>
    public static bool IsExpired(string fileName, DateOnly today)
    {
        if (!fileName.StartsWith(FilePrefix, StringComparison.Ordinal) || !fileName.EndsWith(FileSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var date = fileName[FilePrefix.Length..^FileSuffix.Length];
        return DateOnly.TryParseExact(date, FileDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            && day < today.AddDays(-RetentionDays);
    }
}
