using System.Collections.Concurrent;
using Pizza.Core;
using Pizza.Core.Conversions;
using Pizza.Core.Ipc;
using Pizza.Core.Output;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>Roda as conversões fora da thread da interface e dos hooks, uma de cada vez.</summary>
internal static class Conversion
{
    private const string Profile = "Pizza.Worker";
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2); // SEC-43
    private static readonly ConcurrentQueue<(long Id, ConversionResult Result)> Results = new();
    private static readonly ConcurrentDictionary<long, CancellationTokenSource> Running = new();
    private static readonly ConcurrentDictionary<long, ConversionControl> Controls = new();
    private static readonly ConcurrentDictionary<long, int> Percents = new();
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

        try
        {
            // V-03: o .zip extraído no perfil do usuário não deixa o Worker ler a própria pasta.
            SandboxedProcess.GrantFolder(AppContext.BaseDirectory, Profile);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.ComponentModel.Win32Exception)
        {
            // Pasta protegida (instalada pelo administrador): o instalador já deu o acesso.
        }

        _runner = new ConversionRunner(Path.Combine(AppContext.BaseDirectory, "Pizza.Worker.exe"), Profile, journal, Timeout);
    }

    public static void Start(long id, string sourcePath, ConverterInfo converter, IReadOnlyDictionary<string, string> options, nint notifyWindow, uint notifyMessage, uint progressMessage)
    {
        var cancellation = new CancellationTokenSource();
        Running[id] = cancellation;

        // ADR-029: a porcentagem chega na thread da conversão; o aviso é chamado só quando ela muda.
        var control = new ConversionControl
        {
            Progress = percent =>
            {
                Percents[id] = percent;
                PostMessageW(notifyWindow, progressMessage, (nuint)id, 0);
            },
        };
        Controls[id] = control;
        Task.Run(() =>
        {
            ConversionResult result;
            try
            {
                lock (Gate)
                {
                    result = _runner.Run(sourcePath, converter.Id, converter.Extension, options, cancellation.Token, control, converter.Suffix);
                }
            }
            catch (Exception)
            {
                // Sem isto, um erro inesperado deixaria o aviso preso em "Convertendo".
                result = ConversionResult.Failed("WORKER_CRASHED");
            }

            Running.TryRemove(id, out _);
            Controls.TryRemove(id, out _);
            Percents.TryRemove(id, out _);
            Results.Enqueue((id, result));
            PostMessageW(notifyWindow, notifyMessage, 0, 0);
        });
    }

    /// <summary>ADR-029: congela ou retoma o Worker desta conversão; a que ainda espera na fila é congelada assim que começar.</summary>
    public static void SetPaused(long id, bool paused)
    {
        if (Controls.TryGetValue(id, out var control))
        {
            control.Paused = paused;
        }
    }

    public static int Percent(long id) => Percents.TryGetValue(id, out var percent) ? percent : 0;

    public static void Cancel(long id)
    {
        if (Running.TryGetValue(id, out var cancellation))
        {
            cancellation.Cancel();
        }
    }

    public static (long Id, ConversionResult Result)? TakeResult() => Results.TryDequeue(out var result) ? result : null;
}
