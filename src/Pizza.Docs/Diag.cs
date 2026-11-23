using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Pizza.Docs;

// TEMPORÁRIO (diagnóstico do WORKER_CRASHED). Só liga se existir work\diag-docs.on. Marcas curtas, sem nome nem conteúdo (PRIV-16).
internal static unsafe class Diag
{
    private static string? _file;
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly Lock Gate = new();

    public static void Init(string work)
    {
        if (!File.Exists(Path.Combine(work, "diag-docs.on")))
        {
            return;
        }

        _file = Path.Combine(work, "diag-" + Environment.ProcessId + ".txt");
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Mark("processexit");
        AppDomain.CurrentDomain.UnhandledException += (_, _) => Mark("unhandled");
        _crt_atexit(&NativeAtExit);
    }

    public static void Mark(string text)
    {
        if (_file is null)
        {
            return;
        }

        lock (Gate)
        {
            try
            {
                using var stream = new FileStream(_file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream);
                writer.WriteLine($"{Clock.ElapsedMilliseconds,6} {text}");
                writer.Flush();
                stream.Flush(true);
            }
            catch (IOException)
            {
            }
        }
    }

    public static int Ret(int code, [CallerLineNumber] int line = 0)
    {
        Mark("ret:" + line);
        return code;
    }

    [UnmanagedCallersOnly]
    private static void NativeAtExit() => Mark("native-atexit");

    [DllImport("ucrtbase.dll")]
    private static extern int _crt_atexit(delegate* unmanaged<void> function);
}
