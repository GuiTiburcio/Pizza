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
    private static DocumentEngine? _documents;

    // Adendo do ADR-035: espera do "info" do Worker; sem resposta nesse prazo, o painel mostra "—".
    private static readonly TimeSpan ProbeWait = TimeSpan.FromSeconds(2);
    private static readonly Lock ProbeGate = new();
    private static (string Path, string ConverterId, ProbedWorker Worker)? _probed;
    private static int _probeGeneration;

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

        // ADR-034: o Pizza Jumbo traz o motor de documentos na pasta "motor"; o Brotinho, não.
        var engine = Path.Combine(AppContext.BaseDirectory, "motor");
        var docs = Path.Combine(AppContext.BaseDirectory, "Pizza.Docs.exe");
        if (File.Exists(Path.Combine(engine, "program", "mergedlo.dll")) && File.Exists(docs))
        {
            DocumentEngine.ReleaseStaleDrives(engine);
            try
            {
                DocumentEngine.CleanWorkFolder();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            {
                // Sem limpeza agora: sobras são removidas na próxima vez.
            }

            _documents = new DocumentEngine(docs, engine, journal, Timeout);
            ConverterCatalog.DocumentEngineAvailable = true;
        }
    }

    /// <summary>
    /// ADR-034: o menu abriu sobre um documento; o motor começa a carregar enquanto a pessoa escolhe a fatia.
    /// Fora da thread da interface, porque iniciar o processo leva alguns milissegundos.
    /// </summary>
    public static void WarmUp(string path)
    {
        if (_documents is { } documents && ConverterCatalog.All.Any(converter => converter.DocumentEngine && converter.InputExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)))
        {
            Task.Run(() =>
            {
                try
                {
                    documents.WarmUp();
                }
                catch (Exception)
                {
                    // Sem reserva, a conversão inicia o motor na hora, como antes.
                }
            });
        }
    }

    /// <summary>
    /// Adendo do ADR-035: o painel de um áudio abriu. Um Worker novo recebe o handle do arquivo, devolve duração e
    /// formato e fica esperando; a conversão deste arquivo usa esse mesmo Worker. Avisa a janela quando terminar.
    /// </summary>
    public static void Probe(string path, ConverterInfo converter, nint notifyWindow, uint notifyMessage)
    {
        DiscardProbe();
        var generation = Interlocked.Increment(ref _probeGeneration);
        Task.Run(() =>
        {
            ProbedWorker? probed = null;
            try
            {
                probed = _runner.Probe(path, converter.Id, ProbeWait);
            }
            catch (Exception)
            {
                // Sem probe, o painel fica com "—" e a conversão abre o seu Worker, como antes.
            }

            lock (ProbeGate)
            {
                if (generation != _probeGeneration || probed is null)
                {
                    // O painel já fechou (ou outro abriu): este Worker não vai converter nada.
                    probed?.Dispose();
                    return;
                }

                _probed = (path, converter.Id, probed);
            }

            PostMessageW(notifyWindow, notifyMessage, 0, 0);
        });
    }

    /// <summary>O "info" do probe deste arquivo, se já chegou.</summary>
    public static MediaInfo? ProbeInfo()
    {
        lock (ProbeGate)
        {
            return _probed?.Worker.Info;
        }
    }

    /// <summary>O painel fechou sem converter: o Worker do probe é encerrado.</summary>
    public static void DiscardProbe()
    {
        lock (ProbeGate)
        {
            Interlocked.Increment(ref _probeGeneration);
            _probed?.Worker.Dispose();
            _probed = null;
        }
    }

    private static ProbedWorker? TakeProbe(string path, string converterId)
    {
        lock (ProbeGate)
        {
            Interlocked.Increment(ref _probeGeneration);
            var probed = _probed is { } match && match.Path == path && match.ConverterId == converterId ? match.Worker : null;
            if (probed is null)
            {
                _probed?.Worker.Dispose();
            }

            _probed = null;
            return probed;
        }
    }

    /// <summary>Ao sair: o motor de reserva encerra e leva a letra e a área oculta junto.</summary>
    public static void Shutdown()
    {
        DiscardProbe();
        _documents?.Dispose();
    }

    /// <param name="moreSources">Juntar (ADR-033): os outros arquivos, na ordem do arraste; vazio nas demais.</param>
    public static void Start(long id, string sourcePath, IReadOnlyList<string> moreSources, ConverterInfo converter, IReadOnlyDictionary<string, string> options, nint notifyWindow, uint notifyMessage, uint progressMessage)
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
        var probed = moreSources.Count == 0 ? TakeProbe(sourcePath, converter.Id) : null;
        Task.Run(() =>
        {
            ConversionResult result;
            try
            {
                lock (Gate)
                {
                    result = converter.DocumentEngine
                        ? _documents!.Run(sourcePath, converter.Id, converter.Extension, cancellation.Token, control)
                        : _runner.Run(sourcePath, converter.Id, converter.Extension, options, cancellation.Token, control, converter.Suffix, converter.PartSuffix(options), moreSources.Count == 0 ? null : moreSources, probed);
                }
            }
            catch (Exception)
            {
                // Sem isto, um erro inesperado deixaria o aviso preso em "Convertendo".
                result = ConversionResult.Failed("WORKER_CRASHED");
            }
            finally
            {
                probed?.Dispose();
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
