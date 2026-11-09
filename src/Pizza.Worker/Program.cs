using System.Globalization;
using Microsoft.Win32.SafeHandles;
using Pizza.Core.Ipc;

namespace Pizza.Worker;

internal static class Program
{
    private const int InvalidArguments = 2;
    private const int ProtocolViolation = 3;

    /// <summary>
    /// Worker isolado: fala só pelos dois pipes herdados e encerra diante de qualquer mensagem fora
    /// do contrato (SEC-13). Os conversores chegam na etapa 2c; até lá, todo convert é recusado.
    /// </summary>
    private static int Main(string[] args)
    {
        if (args is not ["--in", var input, "--out", var output])
        {
            return InvalidArguments;
        }

        using var reader = OpenPipe(input, FileAccess.Read);
        using var writer = OpenPipe(output, FileAccess.Write);
        try
        {
            while (IpcFraming.ReadFrame(reader) is { } frame)
            {
                var message = IpcCodec.Parse(frame);
                if (message.Type == IpcType.Convert)
                {
                    IpcFraming.WriteFrame(writer, IpcCodec.Serialize(new IpcMessage(IpcType.Error, message.JobId) { Code = "UNSUPPORTED" }));
                }
            }

            return 0;
        }
        catch (IpcProtocolException)
        {
            return ProtocolViolation;
        }
    }

    private static FileStream OpenPipe(string value, FileAccess access) =>
        new(new SafeFileHandle(nint.Parse(value, CultureInfo.InvariantCulture), ownsHandle: true), access, 0, false);
}
