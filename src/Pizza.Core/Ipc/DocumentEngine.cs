using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Pizza.Core.Output;

namespace Pizza.Core.Ipc;

/// <summary>
/// Motor de documentos do Pizza Jumbo (ADR-034): roda o Pizza.Docs.exe isolado, com o motor LibreOffice na raiz de
/// uma letra temporária e as janelas dele numa área de trabalho oculta. O resto é o caminho de sempre do Worker:
/// handles (SEC-15), gravação segura (SEC-29 a SEC-37), prazo (SEC-43) e cancelamento.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed unsafe partial class DocumentEngine(string docsPath, string engineRoot, TempJournal journal, TimeSpan timeout, string profile = DocumentEngine.Profile) : IDisposable
{
    public const string Profile = "Pizza.Docs";

    private const uint RawTargetPath = 0x1;
    private const uint RemoveDefinition = 0x2;
    private const uint ExactMatchOnRemove = 0x4;
    private const uint NoBroadcastSystem = 0x8;
    private const uint DesktopAll = 0x10000000;

    /// <summary>Quanto o motor de reserva espera por um documento antes de sair sozinho.</summary>
    public TimeSpan SpareIdle { get; init; } = TimeSpan.FromSeconds(60);

    private readonly Lock _gate = new();
    private Spare? _spare;
    private bool _granted;

    /// <summary>
    /// Inicia um motor de reserva, se ainda não houver um: ele carrega o LibreOffice enquanto a pessoa escolhe a
    /// fatia. É usado por um único documento (S-02) e sai sozinho depois de <see cref="SpareIdle"/>.
    /// </summary>
    public void WarmUp()
    {
        lock (_gate)
        {
            if (_spare is { } spare && !spare.Process.WaitForExit(0))
            {
                return;
            }

            _spare?.Release(engineRoot);
            var started = StartSpare();
            _spare = started;

            // O motor sai sozinho no fim da espera; a letra e a área oculta saem com ele, sem esperar o próximo uso.
            started.Expiry = new Timer(_ => ReleaseIfIdle(started), null, SpareIdle + TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Encerra o motor de reserva e libera a letra e a área oculta dele.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _spare?.Release(engineRoot);
            _spare = null;
        }
    }

    private void ReleaseIfIdle(Spare spare)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_spare, spare))
            {
                _spare = null;
                spare.Release(engineRoot);
            }
        }
    }

    /// <summary>Converte um documento com o motor de reserva, ou com um novo; depois deixa outro de reserva.</summary>
    public ConversionResult Run(string sourcePath, string converterId, string extension, CancellationToken cancellation = default, ConversionControl? control = null)
    {
        Spare engine;
        lock (_gate)
        {
            engine = _spare is { } spare && !spare.Process.WaitForExit(0) ? spare : StartSpare();
            if (!ReferenceEquals(engine, _spare))
            {
                _spare?.Release(engineRoot);
            }

            _spare = null;
        }

        try
        {
            var runner = new ConversionRunner(docsPath, profile, journal, timeout, start: () => engine.Process);
            return runner.Run(sourcePath, converterId, extension, cancellation: cancellation, control: control);
        }
        finally
        {
            engine.Release(engineRoot);

            // Quem converte um documento costuma converter outro em seguida.
            WarmUp();
        }
    }

    private Spare StartSpare()
    {
        var sid = SandboxedProcess.EnsureProfile(profile);
        if (!_granted)
        {
            SandboxedProcess.GrantFolder(Path.GetDirectoryName(docsPath)!, profile);
            SandboxedProcess.GrantFolder(RealFolder(engineRoot), profile);
            _granted = true;
        }

        var desktopName = $"Pizza.Docs.{Guid.NewGuid():N}";
        var desktop = CreateHiddenDesktop(desktopName, sid);
        var drive = MapEngine(engineRoot);
        try
        {
            var arguments = $"--engine \"{drive}\\program\" --work \"{ContainerFolder(sid)}\" --idle {(int)SpareIdle.TotalSeconds}";
            var process = SandboxedProcess.Start(docsPath, profile, arguments, @"WinSta0\" + desktopName);
            return new Spare(process, desktop, drive);
        }
        catch
        {
            Unmap(drive, engineRoot);
            CloseDesktop(desktop);
            throw;
        }
    }

    /// <summary>Um motor e o que é só dele: a área oculta e a letra, liberadas juntas.</summary>
    private sealed class Spare(SandboxedProcess process, nint desktop, string drive)
    {
        private bool _released;

        public SandboxedProcess Process => process;

        public Timer? Expiry { get; set; }

        public void Release(string engineRoot)
        {
            if (_released)
            {
                return;
            }

            _released = true;
            Expiry?.Dispose();
            process.Dispose();
            Unmap(drive, engineRoot);
            CloseDesktop(desktop);
        }
    }

    /// <summary>Remove letras que tenham sobrado de uma queda e ainda apontem para o motor. Chamado ao abrir o Pizza.</summary>
    public static void ReleaseStaleDrives(string engineRoot)
    {
        foreach (var drive in DrivesFor(engineRoot))
        {
            Unmap(drive, engineRoot);
        }
    }

    /// <summary>Letras que apontam para a pasta do motor; o Windows guarda o alvo como "\??\caminho".</summary>
    public static IReadOnlyList<string> DrivesFor(string engineRoot)
    {
        var target = Target(engineRoot);
        var found = new List<string>();
        var buffer = new char[1024];
        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            var drive = $"{letter}:";
            var length = QueryDosDeviceW(drive, buffer, buffer.Length);
            if (length > 0 && new string(buffer, 0, Array.IndexOf(buffer, '\0')).Equals(target, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(drive);
            }
        }

        return found;
    }

    /// <summary>
    /// ADR-034: o LibreOffice confere cada pasta acima do motor, e o AppContainer não lista nenhuma delas. Na raiz
    /// de uma letra não há pasta acima. A letra não avisa o Explorer e existe só durante a conversão.
    /// </summary>
    private static string MapEngine(string engineRoot)
    {
        var buffer = new char[16];
        for (var letter = 'Z'; letter >= 'M'; letter--)
        {
            var drive = $"{letter}:";
            if (QueryDosDeviceW(drive, buffer, buffer.Length) == 0 && DefineDosDeviceW(RawTargetPath | NoBroadcastSystem, drive, Target(engineRoot)))
            {
                return drive;
            }
        }

        throw new IOException("Nenhuma letra livre para o motor de documentos.");
    }

    /// <summary>Só remove a letra se ela ainda apontar para o motor; uma letra de outro programa fica intacta.</summary>
    private static void Unmap(string drive, string engineRoot) =>
        DefineDosDeviceW(RawTargetPath | RemoveDefinition | ExactMatchOnRemove | NoBroadcastSystem, drive, Target(engineRoot));

    private static string Target(string engineRoot) => @"\??\" + RealFolder(engineRoot);

    /// <summary>
    /// A pasta real do motor, mesmo atrás de um atalho de pasta (junção ou link simbólico): a permissão dada ao
    /// atalho não vale para o que está dentro dele, e o sandbox não leria o motor.
    /// </summary>
    private static string RealFolder(string folder)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(folder));
        return (directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? directory.FullName).TrimEnd('\\');
    }

    /// <summary>
    /// Ao abrir o Pizza, nenhum motor está rodando: apaga as pastas de documentos que tenham sobrado de um motor
    /// encerrado à força. O perfil-modelo fica.
    /// </summary>
    public static void CleanWorkFolder(string profile = Profile)
    {
        foreach (var folder in Directory.EnumerateDirectories(WorkFolder(profile)))
        {
            if (Path.GetFileName(folder).StartsWith("modelo-", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Tenta de novo na próxima vez que o Pizza abrir.
            }
        }
    }

    /// <summary>Pasta de trabalho do motor: a pasta própria do AppContainer do perfil.</summary>
    public static string WorkFolder(string profile) => ContainerFolder(SandboxedProcess.EnsureProfile(profile));

    /// <summary>Pasta própria do AppContainer: só ele (e a pessoa) gravam ali.</summary>
    private static string ContainerFolder(string sid)
    {
        Marshal.ThrowExceptionForHR(GetAppContainerFolderPath(sid, out var path));
        try
        {
            return Marshal.PtrToStringUni(path)!;
        }
        finally
        {
            Marshal.FreeCoTaskMem(path);
        }
    }

    /// <summary>Área de trabalho invisível: o dono, o sistema e só este AppContainer podem usá-la.</summary>
    private static nint CreateHiddenDesktop(string name, string sid)
    {
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW($"D:(A;;GA;;;OW)(A;;GA;;;SY)(A;;GA;;;{sid})", 1, out var descriptor, 0))
        {
            throw new Win32Exception();
        }

        try
        {
            var attributes = new SecurityAttributes { Length = sizeof(SecurityAttributes), Descriptor = descriptor };
            var desktop = CreateDesktopW(name, 0, 0, 0, DesktopAll, &attributes);
            return desktop != 0 ? desktop : throw new Win32Exception();
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public nint Descriptor;
        public int Inherit;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "DefineDosDeviceW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DefineDosDeviceW(uint flags, string device, string target);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryDosDeviceW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial uint QueryDosDeviceW(string device, [Out] char[] target, int max);

    [LibraryImport("userenv.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetAppContainerFolderPath(string sid, out nint path);

    [LibraryImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string sddl, uint revision, out nint descriptor, nint size);

    [LibraryImport("user32.dll", EntryPoint = "CreateDesktopW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint CreateDesktopW(string name, nint device, nint mode, uint flags, uint access, SecurityAttributes* attributes);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseDesktop(nint desktop);

    [LibraryImport("kernel32.dll")]
    private static partial nint LocalFree(nint memory);
}
