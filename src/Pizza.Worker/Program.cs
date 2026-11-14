using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32.SafeHandles;
using Pizza.Converters;
using Pizza.Core.Conversions;
using Pizza.Core.Diagnostics;
using Pizza.Core.Ipc;

namespace Pizza.Worker;

internal static class Program
{
    private const int InvalidArguments = 2;
    private const int ProtocolViolation = 3;

    // Se a gravação final falhar ao fechar, o Worker cai antes de responder "done" e a saída é descartada.
    private const int DestinationBufferBytes = 1024 * 1024;

    /// <summary>
    /// Worker isolado: fala só pelos dois pipes herdados, converte um único arquivo e encerra (SEC-29).
    /// Qualquer mensagem fora do contrato também o encerra (SEC-13).
    /// </summary>
    private static int Main(string[] args)
    {
        // PRIV-05 (ADR-032): o job já força este modo; aqui fica a segunda barreira.
        CrashReporting.Suppress();
        if (args is not ["--in", var input, "--out", var output])
        {
            return InvalidArguments;
        }

        using var reader = OpenHandle(long.Parse(input, CultureInfo.InvariantCulture), FileAccess.Read);
        using var writer = OpenHandle(long.Parse(output, CultureInfo.InvariantCulture), FileAccess.Write);
        try
        {
            while (IpcFraming.ReadFrame(reader) is { } frame)
            {
                var message = IpcCodec.Parse(frame);
                if (message.Type == IpcType.Probe)
                {
                    // Adendo do ADR-035: responde e fica esperando o "convert" deste mesmo arquivo (ADR-019).
                    IpcFraming.WriteFrame(writer, IpcCodec.Serialize(Probe(message)));
                    continue;
                }

                if (message.Type == IpcType.Convert)
                {
                    var channel = new ProgressChannel(writer, message.JobId);
                    channel.Write(Convert(message, channel));
                    return 0;
                }
            }

            return 0;
        }
        catch (IpcProtocolException)
        {
            return ProtocolViolation;
        }
    }

    private static IpcMessage Probe(IpcMessage request)
    {
        if (ConverterRegistry.Find(request.ConverterId!) is not Pizza.Converters.Audio.AudioConverter)
        {
            return new IpcMessage(IpcType.Error, request.JobId) { Code = "UNSUPPORTED" };
        }

        RequireMicrosoftSigned();
        using var source = OpenHandle(request.SrcHandle!.Value, FileAccess.Read);
        return Pizza.Converters.Audio.AudioConverter.Probe(source) is { } media
            ? new IpcMessage(IpcType.Info, request.JobId) { Media = media }
            : new IpcMessage(IpcType.Error, request.JobId) { Code = "UNSUPPORTED" };
    }

    /// <summary>SEC-26 (ADR-035): só codecs do próprio Windows, antes de o Media Foundation carregar qualquer um.</summary>
    private static void RequireMicrosoftSigned()
    {
        if (!Pizza.Converters.Audio.SignaturePolicy.IsMicrosoftSignedOnly())
        {
            Pizza.Converters.Audio.SignaturePolicy.RequireMicrosoftSigned();
        }
    }

    private static IpcMessage Convert(IpcMessage request, IProgress<int> progress)
    {
        // SPECS §10.3: só o que está no registro, e só com opções dentro do esquema.
        if (ConverterRegistry.Find(request.ConverterId!) is not { } converter
            || ConverterCatalog.Find(converter.Id) is not { } info
            || ConverterCatalog.ResolveOptions(info, request.Options!) is not { } options)
        {
            return new IpcMessage(IpcType.Error, request.JobId) { Code = "UNSUPPORTED" };
        }

        if (converter is Pizza.Converters.Audio.AudioConverter)
        {
            RequireMicrosoftSigned();
        }

        var clock = Stopwatch.StartNew();
        string? error;

        // O destino é fechado antes da resposta: depois de "done", o Worker não tem como escrever.
        using (var source = OpenHandle(request.SrcHandle!.Value, FileAccess.Read))
        using (var destination = OpenHandle(request.DstHandle!.Value, FileAccess.Write, DestinationBufferBytes))
        {
            if (request.MoreSrcHandles is not { } more)
            {
                error = converter.Convert(source, destination, options, progress);
            }
            else if (converter is IMergeConverter merger)
            {
                // Juntar (ADR-033): as outras entradas também chegam só como handles, na ordem do arraste.
                var sources = new List<Stream> { source };
                try
                {
                    sources.AddRange(more.Select(handle => (Stream)OpenHandle(handle, FileAccess.Read)));
                    error = merger.Convert(sources, destination, options, progress);
                }
                finally
                {
                    sources.Skip(1).ToList().ForEach(stream => stream.Dispose());
                }
            }
            else
            {
                error = "UNSUPPORTED";
            }
        }

        return error is null
            ? new IpcMessage(IpcType.Done, request.JobId) { ElapsedMs = clock.ElapsedMilliseconds }
            : new IpcMessage(IpcType.Error, request.JobId) { Code = error };
    }

    private static FileStream OpenHandle(long value, FileAccess access, int bufferBytes = 0) =>
        new(new SafeFileHandle((nint)value, ownsHandle: true), access, bufferBytes, false);

    /// <summary>
    /// ADR-029: só a porcentagem sai pelo canal, uma vez por ponto novo. O libvips avisa de outras threads,
    /// então a escrita é serializada com a resposta final.
    /// </summary>
    private sealed class ProgressChannel(Stream writer, string jobId) : IProgress<int>
    {
        private readonly Lock _gate = new();
        private int _last = -1;

        public void Report(int value)
        {
            var percent = Math.Clamp(value, 0, 100);
            lock (_gate)
            {
                if (percent <= _last)
                {
                    return;
                }

                _last = percent;
                try
                {
                    IpcFraming.WriteFrame(writer, IpcCodec.Serialize(new IpcMessage(IpcType.Progress, jobId) { Percent = percent }));
                }
                catch (IOException)
                {
                    // O residente fechou o canal (cancelou): a conversão é encerrada por ele.
                }
            }
        }

        public void Write(IpcMessage message)
        {
            lock (_gate)
            {
                IpcFraming.WriteFrame(writer, IpcCodec.Serialize(message));
            }
        }
    }
}
