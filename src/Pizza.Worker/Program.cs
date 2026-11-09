using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32.SafeHandles;
using Pizza.Converters;
using Pizza.Core.Conversions;
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
                if (message.Type == IpcType.Convert)
                {
                    IpcFraming.WriteFrame(writer, IpcCodec.Serialize(Convert(message)));
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

    private static IpcMessage Convert(IpcMessage request)
    {
        // SPECS §10.3: só o que está no registro, e só com opções dentro do esquema.
        if (ConverterRegistry.Find(request.ConverterId!) is not { } converter
            || ConverterCatalog.Find(converter.Id) is not { } info
            || ConverterCatalog.ResolveOptions(info, request.Options!) is not { } options)
        {
            return new IpcMessage(IpcType.Error, request.JobId) { Code = "UNSUPPORTED" };
        }

        var clock = Stopwatch.StartNew();
        string? error;

        // O destino é fechado antes da resposta: depois de "done", o Worker não tem como escrever.
        using (var source = OpenHandle(request.SrcHandle!.Value, FileAccess.Read))
        using (var destination = OpenHandle(request.DstHandle!.Value, FileAccess.Write, DestinationBufferBytes))
        {
            error = converter.Convert(source, destination, options);
        }

        return error is null
            ? new IpcMessage(IpcType.Done, request.JobId) { ElapsedMs = clock.ElapsedMilliseconds }
            : new IpcMessage(IpcType.Error, request.JobId) { Code = error };
    }

    private static FileStream OpenHandle(long value, FileAccess access, int bufferBytes = 0) =>
        new(new SafeFileHandle((nint)value, ownsHandle: true), access, bufferBytes, false);
}
