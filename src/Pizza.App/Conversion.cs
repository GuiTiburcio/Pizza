using System.Collections.Concurrent;
using Pizza.Core;
using Pizza.Core.Gesture;
using Pizza.Core.Ipc;
using Pizza.Core.Output;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>Roda as conversões fora da thread da interface e dos hooks, uma de cada vez.</summary>
internal static class Conversion
{
    private const string Profile = "Pizza.Worker";
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2); // SEC-43
    private static readonly ConcurrentQueue<ConversionResult> Results = new();
    private static readonly Lock Gate = new();
    private static ConversionRunner _runner = null!;

    /// <summary>Prepara o diário e limpa temporários de uma queda anterior (SEC-34, SEC-37).</summary>
    public static void Initialize()
    {
        Directory.CreateDirectory(ProductInfo.DataFolder);
        var journal = new TempJournal(Path.Combine(ProductInfo.DataFolder, "temporarios.dat"));
        try
        {
            journal.CleanOrphans();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A limpeza é tentada de novo no próximo início.
        }

        _runner = new ConversionRunner(Path.Combine(AppContext.BaseDirectory, "Pizza.Worker.exe"), Profile, journal, Timeout);
    }

    public static void Start(string sourcePath, MenuSlice slice, nint notifyWindow, uint notifyMessage)
    {
        Task.Run(() =>
        {
            ConversionResult result;
            try
            {
                lock (Gate)
                {
                    result = _runner.Run(sourcePath, slice.ConverterId, slice.Extension);
                }
            }
            catch (Exception)
            {
                // Sem isto, um erro inesperado deixaria o menu preso em "Convertendo".
                result = ConversionResult.Failed("WORKER_CRASHED");
            }

            Results.Enqueue(result);
            PostMessageW(notifyWindow, notifyMessage, 0, 0);
        });
    }

    public static ConversionResult? TakeResult() => Results.TryDequeue(out var result) ? result : null;
}
