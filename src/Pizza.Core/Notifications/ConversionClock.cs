using System.Globalization;

namespace Pizza.Core.Notifications;

/// <summary>
/// Tempo de uma conversão para o aviso (ADR-029): conta só o tempo ativo, estima o que falta pela taxa até agora
/// e descreve o estado. Os instantes vêm de quem chama, em milissegundos, para o cálculo não depender do relógio.
/// </summary>
public sealed class ConversionClock(long startMs)
{
    private const int MinimumPercent = 5;
    private const long MinimumMs = 1000;

    private long _pausedMs;
    private long? _pausedAt;

    public int Percent { get; private set; }

    public bool IsPaused => _pausedAt is not null;

    /// <summary>O Worker só informa avanços; um número menor ou fora de 0 a 100 não faz a linha voltar.</summary>
    public void Report(int percent) => Percent = Math.Clamp(Math.Max(Percent, percent), 0, 100);

    public void Pause(long nowMs) => _pausedAt ??= nowMs;

    public void Resume(long nowMs)
    {
        if (_pausedAt is { } since)
        {
            _pausedMs += nowMs - since;
            _pausedAt = null;
        }
    }

    public long ActiveMs(long nowMs) => Math.Max(0, (_pausedAt ?? nowMs) - startMs - _pausedMs);

    /// <summary>O texto acima da linha verde.</summary>
    public string Text(long nowMs)
    {
        var active = ActiveMs(nowMs);
        if (IsPaused)
        {
            return $"pausado · {Duration(active)} até agora";
        }

        if (Percent < MinimumPercent || active < MinimumMs || Percent >= 100)
        {
            return "calculando o tempo…";
        }

        var remaining = active * (100 - Percent) / Percent;
        return $"{Duration(active)} até agora · falta ~{Duration(remaining)}";
    }

    public string Took(long nowMs)
    {
        var active = ActiveMs(nowMs);
        return active < MinimumMs ? "levou menos de 1 s" : $"levou {Duration(active)}";
    }

    private static string Duration(long ms)
    {
        var seconds = (long)Math.Round(ms / 1000.0);
        return seconds < 60
            ? $"{seconds} s"
            : string.Create(CultureInfo.InvariantCulture, $"{seconds / 60} min {seconds % 60:00} s");
    }
}
