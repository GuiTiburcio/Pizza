using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using Pizza.Core.Output;

namespace Pizza.Core.Ipc;

/// <summary>Resultado de uma conversão: o nome do arquivo criado, ou o código de erro da SPECS §10.4.</summary>
public sealed record ConversionResult(string? OutputName, string? ErrorCode)
{
    public static ConversionResult Failed(string code) => new(null, code);
}

/// <summary>
/// Conduz uma conversão de ponta a ponta. O residente abre os arquivos e entrega só os handles ao
/// Worker (SEC-22); a saída só vira definitiva depois que o Worker encerrou (SEC-29).
/// </summary>
public sealed class ConversionRunner(string workerPath, string profile, TempJournal journal, TimeSpan timeout, string workerArguments = "")
{
    /// <summary>Não é erro do Worker nem vai ao usuário: o cartão só some.</summary>
    public const string Cancelled = "CANCELLED";

    private const uint GenericWrite = 0x40000000;
    private const int ErrorHandleDiskFull = unchecked((int)0x80070027);
    private const int ErrorDiskFull = unchecked((int)0x80070070);
    private const int ExitPollMs = 50;
    private const int PauseCheckMs = 50;
    private static readonly TimeSpan ExitGrace = TimeSpan.FromSeconds(5);

    public ConversionResult Run(string sourcePath, string converterId, string extension, IReadOnlyDictionary<string, string>? options = null, CancellationToken cancellation = default, ConversionControl? control = null)
    {
        if (cancellation.IsCancellationRequested)
        {
            return ConversionResult.Failed(Cancelled);
        }

        try
        {
            using var source = PendingOutput.OpenSource(sourcePath);
            using var output = PendingOutput.Create(source, Path.GetDirectoryName(Path.GetFullPath(sourcePath))!, journal);
            output.CopyZoneFrom(sourcePath);

            var error = RunWorker(source, output.Handle, converterId, options ?? new Dictionary<string, string>(), cancellation, control);
            return error is null
                ? new ConversionResult(output.Commit(OutputNaming.Candidates(sourcePath, extension)), null)
                : ConversionResult.Failed(error);
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
    }

    private string? RunWorker(SafeFileHandle source, SafeFileHandle destination, string converterId, IReadOnlyDictionary<string, string> options, CancellationToken cancellation, ConversionControl? control)
    {
        using var worker = SandboxedProcess.Start(workerPath, profile, workerArguments);
        var jobId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));

        IpcFraming.WriteFrame(worker.Input, IpcCodec.Serialize(new IpcMessage(IpcType.Convert, jobId)
        {
            ConverterId = converterId,
            SrcHandle = worker.DuplicateIntoProcess(source, SandboxedProcess.GenericRead),
            DstHandle = worker.DuplicateIntoProcess(destination, GenericWrite),
            Options = options,
        }));

        // A leitura bloqueia; encerrar o Worker fecha o canal e a libera.
        var reply = Task.Run(() => ReadOutcome(worker.Output, jobId, control?.Progress));
        // ADR-029: pausar congela o Worker, e o prazo do SEC-43 conta só o tempo ativo.
        var active = Stopwatch.StartNew();
        var suspended = false;
        try
        {
            while (!reply.Wait(PauseCheckMs, cancellation))
            {
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
            worker.Kill();
            return error;
        }

        return AwaitExit(worker, cancellation);
    }

    /// <summary>A saída só é confirmada com o Worker encerrado (SEC-29), e cancelar vale até esse instante (FR-29).</summary>
    private static string? AwaitExit(SandboxedProcess worker, CancellationToken cancellation)
    {
        var clock = Stopwatch.StartNew();
        while (!worker.WaitForExit(ExitPollMs))
        {
            if (cancellation.IsCancellationRequested)
            {
                worker.Kill();
                return Cancelled;
            }

            if (clock.Elapsed >= ExitGrace)
            {
                worker.Kill();
                return "WORKER_CRASHED";
            }
        }

        return cancellation.IsCancellationRequested ? Cancelled : null;
    }

    /// <summary>Lê até o resultado. Tudo que vem do Worker é não confiável (SEC-13, SEC-24).</summary>
    private static string? ReadOutcome(Stream output, string jobId, Action<int>? progress)
    {
        try
        {
            while (IpcFraming.ReadFrame(output) is { } frame)
            {
                var message = IpcCodec.Parse(frame);
                if (message.JobId != jobId)
                {
                    return "WORKER_CRASHED";
                }

                switch (message.Type)
                {
                    case IpcType.Done:
                        return null;
                    case IpcType.Error:
                        return message.Code;
                    case IpcType.Progress:
                        progress?.Invoke(message.Percent!.Value);
                        continue;
                    default:
                        return "WORKER_CRASHED";
                }
            }

            return "WORKER_CRASHED";
        }
        catch (Exception ex) when (ex is IpcProtocolException or IOException)
        {
            return "WORKER_CRASHED";
        }
    }
}
