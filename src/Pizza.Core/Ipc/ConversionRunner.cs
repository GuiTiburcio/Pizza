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
public sealed class ConversionRunner(string workerPath, string profile, TempJournal journal, TimeSpan timeout, string workerArguments = "", string? desktop = null, Func<SandboxedProcess>? start = null)
{
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
    /// </summary>
    public ConversionResult Run(string sourcePath, string converterId, string extension, IReadOnlyDictionary<string, string>? options = null, CancellationToken cancellation = default, ConversionControl? control = null, string suffix = "", Func<int, int, string>? parts = null)
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
            if (error is not null)
            {
                return ConversionResult.Failed(error);
            }

            return parts is null
                ? new ConversionResult(output.Commit(OutputNaming.Candidates(sourcePath, extension, suffix)), null)
                : CommitParts(sourcePath, source, output.Handle, extension, parts);
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
    }

    /// <summary>
    /// Copia cada parte do pacote para o seu próprio temporário e só então dá os nomes. Se um nome falhar, as
    /// partes já nomeadas são apagadas: ou saem todas, ou nenhuma (SEC-40, "nada pela metade").
    /// </summary>
    private ConversionResult CommitParts(string sourcePath, SafeFileHandle source, SafeFileHandle bundle, string extension, Func<int, int, string> suffix)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(sourcePath))!;
        var list = OutputBundle.Read(bundle, ConversionResult.MaxParts);
        var pending = new List<PendingOutput>(list.Count);
        var committed = new List<string>(list.Count);
        try
        {
            var buffer = new byte[1024 * 1024];
            foreach (var part in list)
            {
                var output = PendingOutput.Create(source, directory, journal);
                pending.Add(output);
                output.CopyZoneFrom(sourcePath);
                for (long copied = 0; copied < part.Length;)
                {
                    var read = RandomAccess.Read(bundle, buffer.AsSpan(0, (int)Math.Min(buffer.Length, part.Length - copied)), part.Offset + copied);
                    if (read <= 0)
                    {
                        throw new InvalidDataException("Pacote de saída cortado.");
                    }

                    RandomAccess.Write(output.Handle, buffer.AsSpan(0, read), copied);
                    copied += read;
                }
            }

            for (var index = 0; index < pending.Count; index++)
            {
                committed.Add(pending[index].Commit(OutputNaming.Candidates(sourcePath, extension, suffix(index + 1, pending.Count))));
            }

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

    private string? RunWorker(SafeFileHandle source, SafeFileHandle destination, string converterId, IReadOnlyDictionary<string, string> options, CancellationToken cancellation, ConversionControl? control)
    {
        // ADR-034: o motor de documentos pode chegar já aquecido; nenhum outro arquivo passou por ele.
        using var worker = start?.Invoke() ?? SandboxedProcess.Start(workerPath, profile, workerArguments, desktop);
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
        // Só a porcentagem que avança chega à interface: um Worker comprometido não enche a fila de mensagens.
        var last = -1;
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
                        if (message.Percent!.Value > last)
                        {
                            last = message.Percent.Value;
                            progress?.Invoke(last);
                        }

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
