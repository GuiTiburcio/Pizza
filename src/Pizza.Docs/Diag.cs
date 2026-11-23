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
        AddVectoredExceptionHandler(1, &Veh);
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

    [ThreadStatic]
    private static bool _inVeh;
    private static int _vehCount;

    // Códigos de exceção de primeira chance e onde ocorreram (só código e deslocamento no módulo; sem dados).
    [UnmanagedCallersOnly]
    private static int Veh(nint* pointers)
    {
        if (_inVeh || _vehCount > 300)
        {
            return 0;
        }

        _inVeh = true;
        try
        {
            var record = (byte*)pointers[0];
            var code = *(uint*)record;
            if (code is 0xE06D7363 or 0xE0434352 or 0x406D1388 or 0x40010006 or 0x4001000A)
            {
                return 0;
            }

            _vehCount++;
            var address = *(nint*)(record + 16);
            var name = "?";
            var offset = 0L;
            if (GetModuleHandleExW(0x4 | 0x2, address, out var module) && module != 0)
            {
                var buffer = stackalloc char[260];
                var length = GetModuleFileNameW(module, buffer, 260);
                name = Path.GetFileName(new string(buffer, 0, (int)length));
                offset = address - module;
            }

            Mark($"exc:{code:X8} {name}+{offset:X}");
        }
        finally
        {
            _inVeh = false;
        }

        return 0;
    }

    [DllImport("kernel32.dll")]
    private static extern nint AddVectoredExceptionHandler(uint first, delegate* unmanaged<nint*, int> handler);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetModuleHandleExW(uint flags, nint address, out nint module);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetModuleFileNameW(nint module, char* buffer, uint size);

    [UnmanagedCallersOnly]
    private static void NativeAtExit() => Mark("native-atexit");

    [DllImport("ucrtbase.dll")]
    private static extern int _crt_atexit(delegate* unmanaged<void> function);
}
