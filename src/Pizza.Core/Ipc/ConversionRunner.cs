using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using Pizza.Core.Output;

namespace Pizza.Core.Ipc;

/// <summary>
/// Resultado de uma conversão: o nome do arquivo criado, ou o código de erro da SPECS §10.4. Com várias saídas
/// (Dividir), o nome é o da primeira e <paramref name="Count"/> diz quantas foram.
/// </summary>
public sealed record ConversionResult(string? OutputName, string? ErrorCode, int Count = 1)
{
    /// <summary>SEC-42: no máximo uma parte por página.</summary>
    public const int MaxParts = 2000;

    public static ConversionResult Failed(string code) => new(null, code);
}

/// <summary>
/// Conduz uma conversão de ponta a ponta. O residente abre os arquivos e entrega só os handles ao
/// Worker (SEC-22); a saída só vira definitiva depois que o Worker encerrou (SEC-29).
/// </summary>
/// <summary>
/// Worker que já respondeu ao "probe" do painel de áudio e espera o "convert" (adendo do ADR-035). Quem recebe é dono:
/// passa para <see cref="ConversionRunner.Run"/> ou descarta, o que encerra o Worker.
/// </summary>
public sealed class ProbedWorker(SandboxedProcess worker, MediaInfo? info) : IDisposable
{
    private SandboxedProcess? _worker = worker;

    /// <summary>Null quando o Worker recusou o arquivo; a conversão vai dizer o motivo.</summary>
    public MediaInfo? Info { get; } = info;

    internal SandboxedProcess? Take() => Interlocked.Exchange(ref _worker, null);

    public void Dispose() => Take()?.Dispose();
}

public sealed class ConversionRunner(string workerPath, string profile, TempJournal journal, TimeSpan timeout, string workerArguments = "", string? desktop = null, Func<SandboxedProcess>? start = null)
{
    /// <summary>Diagnóstico de testes: somente fase fixa e código de saída, sem dados do arquivo (PRIV-16).</summary>
    internal Action<string, int?>? ObserveFailure { get; init; }

    /// <summary>Não é erro do Worker nem vai ao usuário: o cartão só some.</summary>
    public const string Cancelled = "CANCELLED";

    private const uint GenericWrite = 0x40000000;
    private const int ErrorHandleDiskFull = unchecked((int)0x80070027);
    private const int ErrorDiskFull = unchecked((int)0x80070070);
    private const int ExitPollMs = 50;
    private const int PauseCheckMs = 50;
    private static readonly TimeSpan ExitGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// <paramref name="suffix"/>: entra no nome quando a extensão não muda, como " - girado" (ADR-033).
    /// <paramref name="parts"/>: a saída do Worker é um <see cref="OutputBundle"/>, e cada parte ganha o sufixo
    /// devolvido para (parte, total), contando a partir de 1.
    /// <paramref name="moreSources"/>: Juntar (ADR-033), os outros arquivos na ordem do arraste. Também abertos só
    /// para leitura pelo residente e entregues como handles (SEC-15, SEC-31); a saída fica ao lado do primeiro.
    /// </summary>
    /// <summary>
    /// Adendo do ADR-035: abre a origem só para leitura, entrega o handle a um Worker novo e pede duração e formato.
    /// O Worker fica esperando o "convert". Null se não deu para iniciar; sem resposta a tempo, o Worker é encerrado
    /// e volta só o "—" (Info null, sem Worker).
    /// </summary>
    public ProbedWorker? Probe(string sourcePath, string converterId, TimeSpan wait)
    {
        SandboxedProcess? worker = null;
        try
        {
            using var source = PendingOutput.OpenSource(sourcePath);
            worker = start?.Invoke() ?? SandboxedProcess.Start(workerPath, profile, workerArguments, desktop);
            var jobId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
            IpcFraming.WriteFrame(worker.Input, IpcCodec.Serialize(new IpcMessage(IpcType.Probe, jobId)
            {
                ConverterId = converterId,
                SrcHandle = worker.DuplicateIntoProcess(source, SandboxedProcess.GenericRead),
            }));

            var output = worker.Output;
            var reply = Task.Run(() => ReadInfo(output, jobId));
            if (!reply.Wait(wait) || reply.Result is not (true, var info))
            {
                // Sem resposta, ou resposta fora do contrato: este Worker não serve para converter.
                worker.Dispose();
                return null;
            }

            var probed = new ProbedWorker(worker, info);
            worker = null;
            return probed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            return null;
        }
        finally
        {
            worker?.Dispose();
        }
    }

    /// <summary>Lê só a resposta ao probe: "info", ou "error" (o Worker continua vivo). Qualquer outra coisa reprova.</summary>
    private static (bool Valid, MediaInfo? Info) ReadInfo(Stream output, string jobId)
    {
        try
        {
            if (IpcFraming.ReadFrame(output) is not { } frame || IpcCodec.Parse(frame) is not { } message || message.JobId != jobId)
            {
                return (false, null);
            }

            return message.Type switch
            {
                IpcType.Info => (true, message.Media),
                IpcType.Error => (true, null),
                _ => (false, null),
            };
        }
        catch (Exception ex) when (ex is IpcProtocolException or IOException)
        {
            return (false, null);
        }
    }

    public ConversionResult Run(string sourcePath, string converterId, string extension, IReadOnlyDictionary<string, string>? options = null, CancellationToken cancellation = default, ConversionControl? control = null, string suffix = "", Func<int, int, string>? parts = null, IReadOnlyList<string>? moreSources = null, ProbedWorker? reuse = null)
    {
        if (cancellation.IsCancellationRequested)
        {
            return ConversionResult.Failed(Cancelled);
        }

        var others = new List<SafeFileHandle>();
        try
        {
            using var source = PendingOutput.OpenSource(sourcePath);
            others.AddRange((moreSources ?? []).Select(PendingOutput.OpenSource));
            using var output = PendingOutput.Create(source, Path.GetDirectoryName(Path.GetFullPath(sourcePath))!, journal);

            // SEC-35: no Juntar, a saída leva a zona mais restritiva entre todos os arquivos.
            output.CopyZoneFrom([sourcePath, .. moreSources ?? []]);

            var active = new Stopwatch();
            var limit = OutputBudget.ContainerBytes(parts is not null);
            var error = RunWorker(source, others, output.Handle, converterId, options ?? new Dictionary<string, string>(), cancellation, control, limit, active, reuse?.Take());
            if (error is not null)
            {
                return ConversionResult.Failed(error);
            }

            OutputBudget.Check(RandomAccess.GetLength(output.Handle), limit);
            CheckFinalization(cancellation, active, control);
            return parts is null
                ? new ConversionResult(output.Commit(OutputNaming.Candidates(sourcePath, extension, suffix)), null)
                : CommitParts(sourcePath, source, output.Handle, extension, parts, cancellation, active, control);
        }
        catch (ConversionLimitException)
        {
            return ConversionResult.Failed("LIMIT_EXCEEDED");
        }
        catch (OperationCanceledException)
        {
            return ConversionResult.Failed(Cancelled);
        }
        catch (TimeoutException)
        {
            return ConversionResult.Failed("TIMEOUT");
        }
        catch (InvalidDataException)
        {
            // O Worker entregou um pacote fora do formato: nada é gravado.
            return ConversionResult.Failed("WORKER_CRASHED");
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return ConversionResult.Failed("CORRUPTED");
        }
        catch (UnauthorizedAccessException)
        {
            return ConversionResult.Failed("NO_PERMISSION");
        }
        catch (IOException ex) when (ex.HResult is ErrorDiskFull or ErrorHandleDiskFull)
        {
            return ConversionResult.Failed("NO_SPACE");
        }
        catch (IOException)
        {
            return ConversionResult.Failed("LOCKED");
        }
        catch (Win32Exception)
        {
            return ConversionResult.Failed("WORKER_CRASHED");
        }
        finally
        {
            others.ForEach(handle => handle.Dispose());
            reuse?.Dispose();
        }
    }

    /// <summary>
    /// Copia cada parte do pacote para o seu próprio temporário e só então dá os nomes. Se um nome falhar, as
    /// partes já nomeadas são apagadas: ou saem todas, ou nenhuma (SEC-40, "nada pela metade").
    /// </summary>
    private ConversionResult CommitParts(string sourcePath, SafeFileHandle source, SafeFileHandle bundle, string extension, Func<int, int, string> suffix, CancellationToken cancellation, Stopwatch active, ConversionControl? control)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(sourcePath))!;
        var list = OutputBundle.Read(bundle, ConversionResult.MaxParts, extension);
        var pending = new List<PendingOutput>(list.Count);
        var committed = new List<string>(list.Count);
        try
        {
            // NFR-05: o diário e a zona são feitos uma vez para todas as partes, não uma vez por parte.
            pending.AddRange(PendingOutput.CreateMany(source, directory, journal, list.Count));
            var zone = PendingOutput.ZoneOf(sourcePath);
            var buffer = new byte[1024 * 1024];
            for (var index = 0; index < list.Count; index++)
            {
                CheckFinalization(cancellation, active, control);
                var (part, output) = (list[index], pending[index]);
                output.WriteZone(zone);
                OutputBundle.CopyPart(bundle, part, output.Handle, buffer, () => CheckFinalization(cancellation, active, control));
            }

            PendingOutput.CommitAll(pending, index =>
            {
                CheckFinalization(cancellation, active, control);
                return OutputNaming.Candidates(sourcePath, extension, suffix(index + 1, pending.Count));
            }, committed);
            return new ConversionResult(committed[0], null, committed.Count);
        }
        catch
        {
            foreach (var name in committed)
            {
                File.Delete(Path.Combine(directory, name));
            }

            throw;
        }
        finally
        {
            pending.ForEach(output => output.Dispose());
        }
    }

    internal void CheckFinalization(CancellationToken cancellation, Stopwatch active, ConversionControl? control)
    {
        cancellation.ThrowIfCancellationRequested();
        if (active.Elapsed >= timeout)
        {
            throw new TimeoutException();
        }

        if (control?.Paused == true)
        {
            active.Stop();
            try
            {
                while (control.Paused)
                {
                    cancellation.ThrowIfCancellationRequested();
                    cancellation.WaitHandle.WaitOne(PauseCheckMs);
                }
            }
            finally
            {
                active.Start();
            }
        }

        cancellation.ThrowIfCancellationRequested();
    }

    private string? RunWorker(SafeFileHandle source, IReadOnlyList<SafeFileHandle> others, SafeFileHandle destination, string converterId, IReadOnlyDictionary<string, string> options, CancellationToken cancellation, ConversionControl? control, long limit, Stopwatch active, SandboxedProcess? probed)
    {
        // ADR-034: o motor de documentos pode chegar já aquecido; nenhum outro arquivo passou por ele.
        // Adendo do ADR-035: no áudio, o Worker que respondeu ao probe deste mesmo arquivo.
        using var worker = probed ?? start?.Invoke() ?? SandboxedProcess.Start(workerPath, profile, workerArguments, desktop);
        var jobId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));

        IpcFraming.WriteFrame(worker.Input, IpcCodec.Serialize(new IpcMessage(IpcType.Convert, jobId)
        {
            ConverterId = converterId,
            SrcHandle = worker.DuplicateIntoProcess(source, SandboxedProcess.GenericRead),
            MoreSrcHandles = others.Count == 0 ? null : others.Select(other => worker.DuplicateIntoProcess(other, SandboxedProcess.GenericRead)).ToList(),
            DstHandle = worker.DuplicateIntoProcess(destination, GenericWrite),
            Options = options,
        }));

        // A leitura bloqueia; encerrar o Worker fecha o canal e a libera.
        var reply = Task.Run(() => ReadOutcome(worker.Output, jobId, control?.Progress,
            ObserveFailure is null ? null : stage => ObserveFailure(stage, worker.WaitForExit(0) ? worker.ExitCode : null)));
        // ADR-029: pausar congela o Worker, e o prazo do SEC-43 conta só o tempo ativo.
        active.Start();
        var suspended = false;
        try
        {
            while (!reply.Wait(PauseCheckMs, cancellation))
            {
                if (RandomAccess.GetLength(destination) > limit)
                {
                    worker.Kill();
                    return "LIMIT_EXCEEDED";
                }

                var paused = control?.Paused == true;
                if (paused != suspended)
                {
                    if (paused)
                    {
                        worker.Suspend();
                        active.Stop();
                    }
                    else
                    {
                        worker.Resume();
                        active.Start();
                    }

                    suspended = paused;
                }

                if (active.Elapsed >= timeout)
                {
                    worker.Kill();
                    return "TIMEOUT";
                }
            }

            if (suspended)
            {
                // A resposta já tinha saído quando a pausa chegou: o Worker precisa terminar.
                worker.Resume();
                active.Start();
            }
        }
        catch (OperationCanceledException)
        {
            // FR-29: há um Worker por arquivo, então cancelar é encerrá-lo. A saída temporária é descartada.
            worker.Kill();
            return Cancelled;
        }

        if (reply.Result is { } error)
        {
            if (ObserveFailure is { } observe)
            {
                observe("reply", worker.WaitForExit(100) ? worker.ExitCode : null);
            }
            worker.Kill();
            return RandomAccess.GetLength(destination) > limit ? "LIMIT_EXCEEDED" : error;
        }

        return AwaitExit(worker, cancellation, destination, limit);
    }

    /// <summary>A saída só é confirmada com o Worker encerrado (SEC-29), e cancelar vale até esse instante (FR-29).</summary>
    private string? AwaitExit(SandboxedProcess worker, CancellationToken cancellation, SafeFileHandle destination, long limit)
    {
        var clock = Stopwatch.StartNew();
        while (!worker.WaitForExit(ExitPollMs))
        {
            if (RandomAccess.GetLength(destination) > limit)
            {
                worker.Kill();
                return "LIMIT_EXCEEDED";
            }

            if (cancellation.IsCancellationRequested)
            {
                worker.Kill();
                return Cancelled;
            }

            if (clock.Elapsed >= ExitGrace)
            {
                ObserveFailure?.Invoke("exit-grace", worker.WaitForExit(0) ? worker.ExitCode : null);
                worker.Kill();
                return "WORKER_CRASHED";
            }
        }

        return RandomAccess.GetLength(destination) > limit ? "LIMIT_EXCEEDED"
            : cancellation.IsCancellationRequested ? Cancelled : null;
    }

    /// <summary>Lê até o resultado. Tudo que vem do Worker é não confiável (SEC-13, SEC-24).</summary>
    private static string? ReadOutcome(Stream output, string jobId, Action<int>? progress, Action<string>? failure = null)
    {
        string Failed(string phase)
        {
            failure?.Invoke(phase);
            return "WORKER_CRASHED";
        }

        // Só a porcentagem que avança chega à interface: um Worker comprometido não enche a fila de mensagens.
        var last = -1;
        try
        {
            while (IpcFraming.ReadFrame(output) is { } frame)
            {
                var message = IpcCodec.Parse(frame);
                if (message.JobId != jobId)
                {
                    return Failed("job-id");
                }

                switch (message.Type)
                {
                    case IpcType.Done:
                        return null;
                    case IpcType.Error:
                        return message.Code;
                    case IpcType.Progress:
                        if (message.Percent!.Value > last)
                        {
                            last = message.Percent.Value;
                            progress?.Invoke(last);
                        }

                        continue;
                    default:
                        return Failed("message-type");
                }
            }

            return Failed("end-of-stream");
        }
        catch (Exception ex) when (ex is IpcProtocolException or IOException)
        {
            return Failed(ex is IpcProtocolException ? "protocol" : "pipe-io");
        }
    }
}
