using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Pizza.Core.Ipc;

/// <summary>
/// Processo em AppContainer sem capabilities (SEC-20), dentro de um Job Object (SEC-21), ligado ao
/// residente por pipes sem nome (ADR-021). Sem nome, nenhum outro processo tem como abrir o canal.
/// </summary>
public sealed unsafe partial class SandboxedProcess : IDisposable
{
    public const uint GenericRead = 0x80000000;

    private const int AlreadyExists = unchecked((int)0x800700B7);
    private const ulong MemoryLimitBytes = 1536UL * 1024 * 1024;
    private const int KillWaitMs = 5000;

    private readonly nint _process;
    private readonly nint _job;
    private bool _disposed;

    private SandboxedProcess(nint process, nint job, int processId, Stream input, Stream output)
    {
        _process = process;
        _job = job;
        ProcessId = processId;
        Input = input;
        Output = output;
    }

    /// <summary>Canal de escrita: do residente para o processo.</summary>
    public Stream Input { get; }

    /// <summary>Canal de leitura: do processo para o residente.</summary>
    public Stream Output { get; }

    public int ProcessId { get; }

    public int ExitCode
    {
        get
        {
            Check(GetExitCodeProcess(_process, out var code));
            return (int)code;
        }
    }

    /// <summary>Cria o perfil, se preciso, e devolve o SID dele em texto.</summary>
    public static string EnsureProfile(string name)
    {
        var sid = AcquireSid(name);
        try
        {
            Check(ConvertSidToStringSidW(sid, out var text));
            try
            {
                return Marshal.PtrToStringUni(text)!;
            }
            finally
            {
                LocalFree(text);
            }
        }
        finally
        {
            FreeSid(sid);
        }
    }

    public static void DeleteProfile(string name) => DeleteAppContainerProfile(name);

    /// <summary>
    /// V-03: o Worker em AppContainer só abre se puder ler a própria pasta. Um .zip extraído no perfil do usuário não
    /// dá esse acesso, e toda conversão falharia. Dá leitura e execução, sem escrita, só ao SID do perfil, uma vez.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static void GrantFolder(string folder, string profile)
    {
        var sid = new System.Security.Principal.SecurityIdentifier(EnsureProfile(profile));
        var directory = new DirectoryInfo(folder);
        var acl = directory.GetAccessControl();
        var granted = acl.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(System.Security.Principal.SecurityIdentifier))
            .Cast<System.Security.AccessControl.FileSystemAccessRule>()
            .Any(rule => rule.IdentityReference == sid
                && rule.AccessControlType == System.Security.AccessControl.AccessControlType.Allow
                && rule.FileSystemRights.HasFlag(System.Security.AccessControl.FileSystemRights.ReadAndExecute));
        if (granted)
        {
            return;
        }

        acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
            sid,
            System.Security.AccessControl.FileSystemRights.ReadAndExecute,
            System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
            System.Security.AccessControl.PropagationFlags.None,
            System.Security.AccessControl.AccessControlType.Allow));
        directory.SetAccessControl(acl);
    }

    public static SandboxedProcess Start(string path, string profile, string arguments = "")
    {
        var sid = AcquireSid(profile);
        nint job = 0;
        nint inRead = 0, inWrite = 0, outRead = 0, outWrite = 0;
        nint* attributes = null;
        try
        {
            var inherit = new SECURITY_ATTRIBUTES { Length = (uint)sizeof(SECURITY_ATTRIBUTES), InheritHandle = 1 };
            Check(CreatePipe(&inRead, &inWrite, &inherit, 0));
            Check(CreatePipe(&outRead, &outWrite, &inherit, 0));

            // Só as pontas do processo são herdáveis, e a lista abaixo restringe a herança a elas.
            Check(SetHandleInformation(inWrite, HandleFlagInherit, 0));
            Check(SetHandleInformation(outRead, HandleFlagInherit, 0));

            job = CreateJob();

            nuint size = 0;
            InitializeProcThreadAttributeList(null, 2, 0, &size);
            attributes = (nint*)NativeMemory.Alloc(size);
            Check(InitializeProcThreadAttributeList(attributes, 2, 0, &size));

            var capabilities = new SECURITY_CAPABILITIES { AppContainerSid = sid };
            var inherited = stackalloc nint[2] { inRead, outWrite };
            Check(UpdateProcThreadAttribute(attributes, 0, AttributeSecurityCapabilities, &capabilities, (nuint)sizeof(SECURITY_CAPABILITIES), null, null));
            Check(UpdateProcThreadAttribute(attributes, 0, AttributeHandleList, inherited, (nuint)(2 * sizeof(nint)), null, null));

            var info = default(STARTUPINFOEXW);
            info.Size = (uint)sizeof(STARTUPINFOEXW);
            info.AttributeList = attributes;

            var commandLine = $"\"{path}\" --in {inRead} --out {outWrite} {arguments}".ToCharArray();
            Array.Resize(ref commandLine, commandLine.Length + 1);

            PROCESS_INFORMATION process;
            fixed (char* application = path, command = commandLine)
            {
                Check(CreateProcessW(application, command, null, null, 1, ExtendedStartupInfoPresent | CreateSuspended | CreateNoWindow, null, null, &info, &process));
            }

            if (AssignProcessToJobObject(job, process.Process) == 0)
            {
                var error = Marshal.GetLastPInvokeError();
                TerminateProcess(process.Process, 1);
                CloseHandle(process.Thread);
                CloseHandle(process.Process);
                throw new Win32Exception(error);
            }

            ResumeThread(process.Thread);
            CloseHandle(process.Thread);

            var result = new SandboxedProcess(process.Process, job, (int)process.ProcessId, OpenStream(inWrite, FileAccess.Write), OpenStream(outRead, FileAccess.Read));
            job = 0;
            inWrite = 0;
            outRead = 0;
            return result;
        }
        finally
        {
            if (attributes is not null)
            {
                DeleteProcThreadAttributeList(attributes);
                NativeMemory.Free(attributes);
            }

            CloseIfOpen(inRead);
            CloseIfOpen(outWrite);
            CloseIfOpen(inWrite);
            CloseIfOpen(outRead);
            CloseIfOpen(job);
            FreeSid(sid);
        }
    }

    /// <summary>Entrega um arquivo já aberto ao processo isolado (SEC-22) e devolve o número do handle lá dentro.</summary>
    public long DuplicateIntoProcess(SafeFileHandle handle, uint access)
    {
        nint target;
        Check(DuplicateHandle(GetCurrentProcess(), handle.DangerousGetHandle(), _process, &target, access, 0, 0));
        return target;
    }

    /// <summary>
    /// Encerra e espera sair. TerminateProcess é assíncrono: até o fim, o processo ainda segura os
    /// handles duplicados, e o temporário marcado para exclusão continuaria existindo (SEC-34).
    /// </summary>
    /// <summary>ADR-029: pausa congelando o processo inteiro; o libvips cria threads durante a conversão.</summary>
    public void Suspend() => NtSuspendProcess(_process);

    public void Resume() => NtResumeProcess(_process);

    public void Kill()
    {
        TerminateProcess(_process, 1);
        WaitForExit(KillWaitMs);
    }

    public bool WaitForExit(int milliseconds) => WaitForSingleObject(_process, (uint)milliseconds) == 0;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Input.Dispose();
        Output.Dispose();

        // KILL_ON_JOB_CLOSE: fechar o Job encerra o processo, inclusive se o residente cair.
        CloseHandle(_job);
        CloseHandle(_process);
    }

    private static nint AcquireSid(string name)
    {
        nint sid;
        var result = CreateAppContainerProfile(name, name, name, null, 0, &sid);
        if (result == AlreadyExists)
        {
            result = DeriveAppContainerSidFromAppContainerName(name, &sid);
        }

        Marshal.ThrowExceptionForHR(result);
        return sid;
    }

    private static nint CreateJob()
    {
        var job = CreateJobObjectW(null, null);
        if (job == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var limits = default(JOBOBJECT_EXTENDED_LIMIT_INFORMATION);
        limits.Basic.LimitFlags = JobLimitActiveProcess | JobLimitProcessMemory | JobLimitKillOnClose;
        limits.Basic.ActiveProcessLimit = 1;
        limits.ProcessMemoryLimit = (nuint)MemoryLimitBytes;
        Check(SetInformationJobObject(job, JobObjectExtendedLimitInformation, &limits, (uint)sizeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION)));
        return job;
    }

    private static FileStream OpenStream(nint handle, FileAccess access) =>
        new(new SafeFileHandle(handle, ownsHandle: true), access, 0, false);

    private static void CloseIfOpen(nint handle)
    {
        if (handle != 0)
        {
            CloseHandle(handle);
        }
    }

    private static void Check(int result)
    {
        if (result == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    private const uint HandleFlagInherit = 1;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateSuspended = 0x00000004;
    // PRIV-05 (ADR-032): sem CREATE_DEFAULT_ERROR_MODE. O Worker herda do residente o modo sem WER antes de rodar
    // qualquer código; o job não consegue impor esse modo.
    private const uint CreateNoWindow = 0x08000000;
    private const uint JobLimitActiveProcess = 0x00000008;
    private const uint JobLimitProcessMemory = 0x00000100;
    private const uint JobLimitKillOnClose = 0x00002000;
    private const int JobObjectExtendedLimitInformation = 9;
    private static readonly nuint AttributeHandleList = 0x00020002;
    private static readonly nuint AttributeSecurityCapabilities = 0x00020009;

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public uint Length;
        public nint Descriptor;
        public int InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_CAPABILITIES
    {
        public nint AppContainerSid;
        public nint Capabilities;
        public uint CapabilityCount;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEXW
    {
        public uint Size;
        public nint Reserved;
        public nint Desktop;
        public nint Title;
        public uint X;
        public uint Y;
        public uint XSize;
        public uint YSize;
        public uint XCountChars;
        public uint YCountChars;
        public uint FillAttribute;
        public uint Flags;
        public ushort ShowWindow;
        public ushort Reserved2Size;
        public nint Reserved2;
        public nint StdInput;
        public nint StdOutput;
        public nint StdError;
        public nint* AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public nint Process;
        public nint Thread;
        public uint ProcessId;
        public uint ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION Basic;
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    [LibraryImport("userenv.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int CreateAppContainerProfile(string name, string displayName, string description, void* capabilities, uint count, nint* sid);

    [LibraryImport("userenv.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int DeriveAppContainerSidFromAppContainerName(string name, nint* sid);

    [LibraryImport("userenv.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int DeleteAppContainerProfile(string name);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    private static partial int ConvertSidToStringSidW(nint sid, out nint text);

    [LibraryImport("advapi32.dll")]
    private static partial nint FreeSid(nint sid);

    [LibraryImport("ntdll.dll")]
    private static partial int NtSuspendProcess(nint process);

    [LibraryImport("ntdll.dll")]
    private static partial int NtResumeProcess(nint process);

    [LibraryImport("kernel32.dll")]
    private static partial nint LocalFree(nint memory);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int CreatePipe(nint* read, nint* write, SECURITY_ATTRIBUTES* attributes, uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int SetHandleInformation(nint handle, uint mask, uint flags);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int InitializeProcThreadAttributeList(nint* list, uint count, uint flags, nuint* size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int UpdateProcThreadAttribute(nint* list, uint flags, nuint attribute, void* value, nuint size, void* previous, nuint* returned);

    [LibraryImport("kernel32.dll")]
    private static partial void DeleteProcThreadAttributeList(nint* list);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int CreateProcessW(char* application, char* commandLine, void* processAttributes, void* threadAttributes,
        int inheritHandles, uint flags, void* environment, char* directory, STARTUPINFOEXW* startup, PROCESS_INFORMATION* information);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateJobObjectW(void* attributes, string? name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int SetInformationJobObject(nint job, int infoClass, void* info, uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int AssignProcessToJobObject(nint job, nint process);

    [LibraryImport("kernel32.dll")]
    private static partial uint ResumeThread(nint thread);

    [LibraryImport("kernel32.dll")]
    private static partial int TerminateProcess(nint process, uint exitCode);

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int DuplicateHandle(nint sourceProcess, nint source, nint targetProcess, nint* target, uint access, int inherit, uint options);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int GetExitCodeProcess(nint process, out uint exitCode);

    [LibraryImport("kernel32.dll")]
    private static partial uint WaitForSingleObject(nint handle, uint milliseconds);

    [LibraryImport("kernel32.dll")]
    private static partial int CloseHandle(nint handle);
}
