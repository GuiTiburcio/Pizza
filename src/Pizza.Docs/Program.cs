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

    // SEC-62 e SEC-63: macros desligadas, segurança "Muito alta", sem conteúdo remoto. Regravado em todo perfil.
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
    /// Pizza.Docs: inicia o motor assim que abre (aquecimento, ADR-034) e só então espera o pedido pelos pipes
    /// herdados. Converte um único arquivo, recebido por handle (SEC-15), e encerra (S-02). Sem pedido dentro do
    /// prazo de espera, encerra sozinho.
    /// </summary>
    private static int Main(string[] args)
    {
        // PRIV-05 (ADR-032): sem dump nem relatório do Windows.
        CrashReporting.Suppress();
        if (args is not ["--in", var input, "--out", var output, "--engine", var engine, "--work", var work, "--idle", var idle])
        {
            return InvalidArguments;
        }

        using var reader = OpenHandle(long.Parse(input, CultureInfo.InvariantCulture), FileAccess.Read);
        using var writer = OpenHandle(long.Parse(output, CultureInfo.InvariantCulture), FileAccess.Write);
        var folder = Path.Combine(work, Guid.NewGuid().ToString("N"));
        try
        {
            if (!StartEngine(engine, work, folder))
            {
                return EngineUnavailable;
            }

            // Ninguém pediu dentro do prazo: o motor de reserva sai e leva a pasta junto.
            using var expiry = new Timer(_ => Exit(folder), null, TimeSpan.FromSeconds(int.Parse(idle, CultureInfo.InvariantCulture)), Timeout.InfiniteTimeSpan);
            while (IpcFraming.ReadFrame(reader) is { } frame)
            {
                var message = IpcCodec.Parse(frame);
                if (message.Type == IpcType.Convert)
                {
                    expiry.Change(Timeout.Infinite, Timeout.Infinite);
                    IpcFraming.WriteFrame(writer, IpcCodec.Serialize(Convert(message, folder)));
                    return 0;
                }
            }

            return 0;
        }
        catch (IpcProtocolException)
        {
            return ProtocolViolation;
        }
        finally
        {
            Delete(folder);
        }
    }

    /// <summary>
    /// Perfil novo, copiado do modelo quando ele existe. O modelo é criado logo depois de iniciar, antes de
    /// qualquer documento passar pelo motor: assim nenhum documento consegue alterá-lo.
    /// </summary>
    private static bool StartEngine(string engine, string work, string folder)
    {
        var profile = Path.Combine(folder, "perfil");
        var template = Path.Combine(work, "modelo-" + EngineKey(engine));
        var hasTemplate = Directory.Exists(template);
        if (hasTemplate)
        {
            CopyFolder(template, profile);
        }

        Directory.CreateDirectory(Path.Combine(profile, "user"));
        File.WriteAllText(Path.Combine(profile, "user", "registrymodifications.xcu"), LockedProfile);
        if (!LibreOfficeKit.Start(engine, new Uri(profile).AbsoluteUri))
        {
            return false;
        }

        if (!hasTemplate)
        {
            // Cópia completa numa pasta temporária, renomeada no fim: nunca há modelo pela metade.
            var pending = template + "." + Guid.NewGuid().ToString("N");
            try
            {
                CopyFolder(profile, pending);
                Directory.Move(pending, template);
            }
            catch (IOException)
            {
                // Outro motor criou o modelo ao mesmo tempo, ou o disco recusou: segue sem modelo.
                Delete(pending);
            }
        }

        return true;
    }

    private static IpcMessage Convert(IpcMessage request, string folder)
    {
        if (!Targets.TryGetValue(request.ConverterId!, out var target) || request.Options!.Count > 0)
        {
            return new IpcMessage(IpcType.Error, request.JobId) { Code = "UNSUPPORTED" };
        }

        var clock = Stopwatch.StartNew();
        var source = Path.Combine(folder, "entrada");
        var result = Path.Combine(folder, "saida" + target.Extension);
        using (var original = OpenHandle(request.SrcHandle!.Value, FileAccess.Read))
        using (var copy = File.Create(source))
        {
            original.CopyTo(copy);
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

    /// <summary>O modelo vale para uma versão do motor: outra versão cria o seu.</summary>
    private static string EngineKey(string engine)
    {
        var library = new FileInfo(Path.Combine(engine, "mergedlo.dll"));
        return $"{library.Length:x}-{library.LastWriteTimeUtc.Ticks:x}";
    }

    private static void CopyFolder(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == ".lock")
            {
                continue;
            }

            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void Exit(string folder)
    {
        Delete(folder);
        Environment.Exit(0);
    }

    private static void Delete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // O residente limpa as pastas que sobrarem ao abrir.
        }
    }

    private static FileStream OpenHandle(long value, FileAccess access) =>
        new(new SafeFileHandle((nint)value, ownsHandle: true), access, 0, false);
}
