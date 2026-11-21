using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace Pizza.Core.Ipc;

// TEMPORARIO (diagnostico do WORKER_CRASHED): remover antes do commit final. So numeros, sem nomes (PRIV-16).
public static class DiagTemp
{
    public static volatile string? Last;
    public static readonly ConcurrentDictionary<int, string> Drive = new();
    public static readonly ConcurrentDictionary<int, DateTime> Started = new();

    public static string Collect(int pid, int exitCode, bool exited, long sinceSentMs)
    {
        var others = new List<string>();
        foreach (var name in new[] { "Pizza.Docs", "Pizza.Worker", "testhost", "soffice.bin", "WerFault" })
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try
                {
                    others.Add($"{name}:{p.Id}:{(DateTime.Now - p.StartTime).TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)}s");
                }
                catch (Exception)
                {
                    others.Add($"{name}:{p.Id}:?");
                }
            }
        }

        Drive.TryGetValue(pid, out var drive);
        var age = Started.TryGetValue(pid, out var started) ? (DateTime.UtcNow - started).TotalMilliseconds : -1;
        return $"pid={pid} exit=0x{exitCode:X8} exited={exited} sinceSentMs={sinceSentMs} ageMs={age:F0} drive={drive} procs=[{string.Join(' ', others)}]";
    }
}
