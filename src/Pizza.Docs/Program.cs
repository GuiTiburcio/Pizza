using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32.SafeHandles;
using Pizza.Core.Diagnostics;
using Pizza.Core.Ipc;

namespace Pizza.Docs;

internal static class Program
{
    private const int InvalidArguments = 2;
    private const int ProtocolViolation = 3;
    private const int EngineUnavailable = 4;

    // ADR-034: só estas conversões passam pelo motor; o resto é recusado.
    private static readonly Dictionary<string, (string Filter, string Extension)> Targets = new()
    {
        ["document.pdf"] = ("pdf", ".pdf"),
    };

    // SEC-62 e SEC-63: perfil novo a cada arquivo, macros desligadas, segurança "Muito alta" e sem conteúdo remoto.
    private const string LockedProfile = """
        <?xml version="1.0" encoding="UTF-8"?>
        <oor:items xmlns:oor="http://openoffice.org/2001/registry" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
        <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop></item>
        <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="DisableMacrosExecution" oor:op="fuse"><value>true</value></prop></item>
        <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="BlockUntrustedRefererLinks" oor:op="fuse"><value>true</value></prop></item>
        <item oor:path="/org.openoffice.Office.Common/Misc"><prop oor:name="UseOpenCL" oor:op="fuse"><value>false</value></prop></item>
        <item oor:path="/org.openoffice.Office.Writer/Content/Update"><prop oor:name="Link" oor:op="fuse"><value>2</value></prop></item>
        <item oor:path="/org.openoffice.Office.Calc/Content/Update"><prop oor:name="Link" oor:op="fuse"><value>2</value></prop></item>
        </oor:items>
        """;

    /// <summary>
    /// Pizza.Docs: fala pelos dois pipes herdados como o Worker, recebe o documento e a saída por handle (SEC-15),
    /// converte um único arquivo dentro da própria pasta do AppContainer e encerra (SEC-29).
    /// </summary>
    private static int Main(string[] args)
    {
        // PRIV-05 (ADR-032): sem dump nem relatório do Windows.
        CrashReporting.Suppress();
        if (args is not ["--in", var input, "--out", var output, "--engine", var engine, "--work", var work])
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
                    IpcFraming.WriteFrame(writer, IpcCodec.Serialize(Convert(message, engine, work)));
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

    private static IpcMessage Convert(IpcMessage request, string engine, string work)
    {
        if (!Targets.TryGetValue(request.ConverterId!, out var target) || request.Options!.Count > 0)
        {
            return new IpcMessage(IpcType.Error, request.JobId) { Code = "UNSUPPORTED" };
        }

        var clock = Stopwatch.StartNew();
        var folder = Path.Combine(work, request.JobId);
        var profile = Path.Combine(folder, "perfil");
        var source = Path.Combine(folder, "entrada");
        var result = Path.Combine(folder, "saida" + target.Extension);
        try
        {
            Directory.CreateDirectory(Path.Combine(profile, "user"));
            File.WriteAllText(Path.Combine(profile, "user", "registrymodifications.xcu"), LockedProfile);
            using (var original = OpenHandle(request.SrcHandle!.Value, FileAccess.Read))
            using (var copy = File.Create(source))
            {
                original.CopyTo(copy);
            }

            if (!LibreOfficeKit.Start(engine, new Uri(profile).AbsoluteUri))
            {
                Environment.Exit(EngineUnavailable);
            }

            var error = LibreOfficeKit.Convert(source, result, target.Filter);
            if (error is null)
            {
                // O destino é fechado antes da resposta: depois de "done", nada mais é escrito.
                using var destination = OpenHandle(request.DstHandle!.Value, FileAccess.Write);
                using var converted = File.OpenRead(result);
                converted.CopyTo(destination);
            }

            return error is null
                ? new IpcMessage(IpcType.Done, request.JobId) { ElapsedMs = clock.ElapsedMilliseconds }
                : new IpcMessage(IpcType.Error, request.JobId) { Code = error };
        }
        finally
        {
            // A cópia do documento e o perfil não ficam para trás.
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
                // O residente limpa a pasta inteira do sandbox ao abrir.
            }
        }
    }

    private static FileStream OpenHandle(long value, FileAccess access) =>
        new(new SafeFileHandle((nint)value, ownsHandle: true), access, 0, false);
}
