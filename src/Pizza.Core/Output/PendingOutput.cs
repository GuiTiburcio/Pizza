using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Pizza.Core.Output;

/// <summary>
/// Arquivo de saída em construção: um temporário oculto na pasta de destino, que vira o arquivo final
/// por renomeação sem substituir nada. Se não for concluído, ele é apagado (SEC-30 a SEC-34).
/// </summary>
public sealed unsafe class PendingOutput : IDisposable
{
    private const string ZoneStream = ":Zone.Identifier";

    private readonly TempJournal _journal;
    private readonly TempJournal.Entry _entry;
    private readonly string _temporaryPath;
    private bool _finished;

    private PendingOutput(SafeFileHandle handle, string temporaryPath, TempJournal journal, TempJournal.Entry entry)
    {
        Handle = handle;
        _temporaryPath = temporaryPath;
        _journal = journal;
        _entry = entry;
    }

    /// <summary>Handle de escrita, a ser duplicado para o Worker.</summary>
    public SafeFileHandle Handle { get; }

    /// <summary>Abre o original só para leitura, impedindo que outro programa o altere durante a conversão (SEC-31).</summary>
    public static SafeFileHandle OpenSource(string path) =>
        File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);

    /// <summary>Cria o temporário e confirma, pelo handle, que ele está mesmo na pasta do original (SEC-32).</summary>
    public static PendingOutput Create(SafeFileHandle source, string directory, TempJournal journal)
    {
        var temporaryPath = Path.Combine(Path.GetFullPath(directory), OutputNaming.TemporaryName());
        var handle = FileNative.CreateNewHidden(temporaryPath);
        try
        {
            if (!SameDirectory(FileNative.FinalPath(source), FileNative.FinalPath(handle)))
            {
                throw new UnauthorizedAccessException("A pasta de destino aponta para outro lugar.");
            }

            var entry = FileNative.Identify(handle, directory);
            journal.Add(entry);
            return new PendingOutput(handle, temporaryPath, journal, entry);
        }
        catch
        {
            FileNative.MarkForDeletion(handle);
            handle.Dispose();
            throw;
        }
    }

    /// <summary>Passa a marca de internet do original para a saída, se houver (SEC-35). Diz se havia marca.</summary>
    public bool CopyZoneFrom(string sourcePath)
    {
        string content;
        try
        {
            content = File.ReadAllText(sourcePath + ZoneStream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        if (ZoneIdentifier.ParseZone(content) is not { } zone)
        {
            return false;
        }

        // FileShare.Delete é exigido porque o temporário segue aberto com acesso de renomear.
        using var stream = new FileStream(_temporaryPath + ZoneStream, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete);
        stream.Write(System.Text.Encoding.ASCII.GetBytes(ZoneIdentifier.Format(zone)));
        return true;
    }

    /// <summary>
    /// Torna o arquivo definitivo com o primeiro nome livre e devolve esse nome. A renomeação nunca
    /// substitui: se o nome surgir no meio do caminho, passa para o próximo (SEC-33).
    /// </summary>
    public string Commit(IEnumerable<string> candidates)
    {
        ObjectDisposedException.ThrowIf(_finished, this);
        FileNative.ClearHidden(Handle);
        foreach (var name in candidates)
        {
            if (FileNative.TryRename(Handle, name))
            {
                _finished = true;
                Handle.Dispose();
                _journal.Remove(_entry);
                return name;
            }
        }

        throw new IOException("Nenhum nome de saída disponível.");
    }

    public void Dispose()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        FileNative.MarkForDeletion(Handle);
        Handle.Dispose();
        _journal.Remove(_entry);
    }

    /// <summary>Só para testes: solta o arquivo sem apagar, como numa queda do residente.</summary>
    internal void AbandonForTest()
    {
        _finished = true;
        Handle.Dispose();
    }

    private static bool SameDirectory(string first, string second) =>
        string.Equals(Path.GetDirectoryName(first), Path.GetDirectoryName(second), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Chamadas Win32 de arquivo que a API do .NET não expõe.</summary>
internal static unsafe partial class FileNative
{
    private const uint GenericReadWrite = 0xC0000000;
    private const uint Delete = 0x00010000;
    private const uint ReadControl = 0x00020000;
    private const uint ReadAttributes = 0x0080;
    private const uint CreateNew = 1;
    private const uint AttributeHidden = 0x02;
    private const uint AttributeNormal = 0x80;
    private const uint FlagOpenReparsePoint = 0x00200000;
    private const uint FlagBackupSemantics = 0x02000000;
    private const uint OpenExisting = 3;
    private const uint ShareAll = 7;
    private const int FileBasicInfo = 0;
    private const int FileRenameInformation = 10;
    private const int FileDispositionInfo = 4;
    private const int FileIdInfo = 18;
    private const int StatusObjectNameCollision = unchecked((int)0xC0000035);
    private const int ErrorFileNotFound = 2;
    private const int ErrorAccessDenied = 5;
    private const int ErrorInvalidParameter = 87;
    private const int ExtendedFileIdType = 2;
    private const int FileNameOffset = 20;
    private const int OwnerSecurityInformation = 1;
    private const int SeFileObject = 1;
    private const int TokenOwner = 4;
    private const uint TokenQuery = 8;

    public static SafeFileHandle CreateNewHidden(string path)
    {
        var handle = CreateFileW(ToExtended(path), GenericReadWrite | Delete, 0, 0, CreateNew, AttributeHidden, 0);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            if (error == ErrorAccessDenied)
            {
                throw new UnauthorizedAccessException("Sem permissão para gravar nesta pasta.");
            }

            throw new IOException("Não foi possível criar o arquivo temporário.", unchecked((int)(0x80070000 | (uint)error)));
        }

        return handle;
    }

    public static string FinalPath(SafeFileHandle handle)
    {
        var length = GetFinalPathNameByHandleW(handle, null, 0, 0);
        Check(length != 0);
        var buffer = new char[length];
        fixed (char* pointer = buffer)
        {
            length = GetFinalPathNameByHandleW(handle, pointer, length, 0);
        }

        Check(length != 0);
        return new string(buffer, 0, (int)length);
    }

    public static void ClearHidden(SafeFileHandle handle)
    {
        var info = default(FILE_BASIC_INFO);
        info.Attributes = AttributeNormal;
        Check(SetFileInformationByHandle(handle, FileBasicInfo, &info, (uint)sizeof(FILE_BASIC_INFO)) != 0);
    }

    public static bool MarkForDeletion(SafeFileHandle handle)
    {
        byte delete = 1;
        return SetFileInformationByHandle(handle, FileDispositionInfo, &delete, 1) != 0;
    }

    /// <summary>Renomeia na própria pasta, sem substituir. Devolve falso se o nome já existir.</summary>
    public static bool TryRename(SafeFileHandle handle, string name)
    {
        // Só o nome, pela API nativa: o arquivo não sai da pasta dele. A API Win32 resolveria o
        // nome em relação à pasta atual do processo.
        var nameBytes = name.Length * sizeof(char);
        var size = FileNameOffset + nameBytes + sizeof(char);
        var buffer = stackalloc byte[size];
        new Span<byte>(buffer, size).Clear();
        *(uint*)(buffer + 16) = (uint)nameBytes;
        fixed (char* source = name)
        {
            Buffer.MemoryCopy(source, buffer + FileNameOffset, nameBytes, nameBytes);
        }

        IO_STATUS_BLOCK status;
        var result = NtSetInformationFile(handle, &status, buffer, (uint)size, FileRenameInformation);
        if (result == 0)
        {
            return true;
        }

        return result == StatusObjectNameCollision ? false : throw new Win32Exception(RtlNtStatusToDosError(result));
    }

    public static TempJournal.Entry Identify(SafeFileHandle handle, string directory)
    {
        var info = default(FILE_ID_INFO);
        Check(GetFileInformationByHandleEx(handle, FileIdInfo, &info, (uint)sizeof(FILE_ID_INFO)) != 0);
        return new TempJournal.Entry(Path.GetPathRoot(Path.GetFullPath(directory))!, info.VolumeSerialNumber, new ReadOnlySpan<byte>(info.FileId, 16).ToArray());
    }

    /// <summary>
    /// Apaga pelo File ID, sem seguir reparse point, só se for um temporário nosso (SEC-37).
    /// Devolve verdadeiro se o registro pode sair do diário; falso para tentar de novo depois.
    /// </summary>
    public static bool TryResolveOrphan(TempJournal.Entry entry)
    {
        using var volume = CreateFileW(ToExtended(entry.Root), ReadAttributes, ShareAll, 0, OpenExisting, FlagBackupSemantics, 0);
        if (volume.IsInvalid)
        {
            return false;
        }

        var descriptor = new FILE_ID_DESCRIPTOR { Size = (uint)sizeof(FILE_ID_DESCRIPTOR), Type = ExtendedFileIdType };
        entry.FileId.CopyTo(new Span<byte>(descriptor.FileId, 16));
        using var file = OpenFileById(volume, &descriptor, ReadControl | ReadAttributes, ShareAll, 0, FlagOpenReparsePoint);
        if (file.IsInvalid)
        {
            return Marshal.GetLastPInvokeError() is ErrorFileNotFound or ErrorInvalidParameter;
        }

        if (!IsEntry(file, entry))
        {
            return false;
        }

        var path = FinalPath(file);
        if (!IsOwnedByCurrentUser(file) || !Path.GetFileName(path).StartsWith(".~pizza-", StringComparison.Ordinal))
        {
            return true;
        }

        // O NTFS não apaga por um handle aberto por ID: reabre pelo caminho e confere de novo o ID
        // no handle que de fato vai apagar.
        using var target = CreateFileW(ToExtended(path), Delete | ReadAttributes, ShareAll, 0, OpenExisting, FlagOpenReparsePoint, 0);
        return !target.IsInvalid && IsEntry(target, entry) && MarkForDeletion(target);
    }

    private static bool IsEntry(SafeFileHandle handle, TempJournal.Entry entry)
    {
        var info = default(FILE_ID_INFO);
        return GetFileInformationByHandleEx(handle, FileIdInfo, &info, (uint)sizeof(FILE_ID_INFO)) != 0
            && info.VolumeSerialNumber == entry.VolumeSerial
            && new ReadOnlySpan<byte>(info.FileId, 16).SequenceEqual(entry.FileId);
    }

    public static byte[] Protect(byte[] data) => Transform(data, protect: true);

    public static byte[] Unprotect(byte[] data) => Transform(data, protect: false);

    private static byte[] Transform(byte[] data, bool protect)
    {
        fixed (byte* pointer = data)
        {
            var input = new DATA_BLOB { Size = (uint)data.Length, Data = pointer };
            DATA_BLOB output;
            var ok = protect
                ? CryptProtectData(&input, null, null, null, null, 0, &output)
                : CryptUnprotectData(&input, null, null, null, null, 0, &output);
            Check(ok != 0);
            try
            {
                return new ReadOnlySpan<byte>(output.Data, (int)output.Size).ToArray();
            }
            finally
            {
                LocalFree(output.Data);
            }
        }
    }

    private static bool IsOwnedByCurrentUser(SafeFileHandle file)
    {
        nint owner;
        nint descriptor;
        if (GetSecurityInfo(file, SeFileObject, OwnerSecurityInformation, &owner, null, null, null, &descriptor) != 0)
        {
            return false;
        }

        try
        {
            nint token;
            if (OpenProcessToken(GetCurrentProcess(), TokenQuery, &token) == 0)
            {
                return false;
            }

            try
            {
                // Dono padrão do token: é quem fica como dono dos arquivos que este usuário cria.
                var buffer = stackalloc byte[128];
                uint returned;
                return GetTokenInformation(token, TokenOwner, buffer, 128, &returned) != 0
                    && EqualSid(*(nint*)buffer, owner) != 0;
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            LocalFree((void*)descriptor);
        }
    }

    private static string ToExtended(string path)
    {
        const string extended = @"\\?\";
        if (path.StartsWith(extended, StringComparison.Ordinal))
        {
            return path;
        }

        return path.StartsWith(@"\\", StringComparison.Ordinal) ? extended + "UNC" + path[1..] : extended + path;
    }

    private static void Check(bool ok)
    {
        if (!ok)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_STATUS_BLOCK
    {
        public nint Status;
        public nint Information;
    }

    [LibraryImport("ntdll.dll")]
    private static partial int NtSetInformationFile(SafeFileHandle handle, IO_STATUS_BLOCK* status, void* info, uint size, int infoClass);

    [LibraryImport("ntdll.dll")]
    private static partial int RtlNtStatusToDosError(int status);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_BASIC_INFO
    {
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public long ChangeTime;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_ID_INFO
    {
        public ulong VolumeSerialNumber;
        public fixed byte FileId[16];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_ID_DESCRIPTOR
    {
        public uint Size;
        public int Type;
        public fixed byte FileId[16];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public uint Size;
        public byte* Data;
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint GetFinalPathNameByHandleW(SafeFileHandle handle, char* buffer, uint size, uint flags);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int SetFileInformationByHandle(SafeFileHandle handle, int infoClass, void* info, uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass, void* info, uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeFileHandle OpenFileById(SafeFileHandle volume, FILE_ID_DESCRIPTOR* id, uint access, uint share, nint security, uint flags);

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll")]
    private static partial int CloseHandle(nint handle);

    [LibraryImport("kernel32.dll")]
    private static partial nint LocalFree(void* memory);

    [LibraryImport("advapi32.dll")]
    private static partial int OpenProcessToken(nint process, uint access, nint* token);

    [LibraryImport("advapi32.dll")]
    private static partial int GetTokenInformation(nint token, int infoClass, void* info, uint size, uint* returned);

    [LibraryImport("advapi32.dll")]
    private static partial int EqualSid(nint first, nint second);

    [LibraryImport("advapi32.dll")]
    private static partial uint GetSecurityInfo(SafeFileHandle handle, int objectType, uint info, nint* owner, nint* group, nint* dacl, nint* sacl, nint* descriptor);

    [LibraryImport("crypt32.dll", SetLastError = true)]
    private static partial int CryptProtectData(DATA_BLOB* input, char* description, DATA_BLOB* entropy, void* reserved, void* prompt, uint flags, DATA_BLOB* output);

    [LibraryImport("crypt32.dll", SetLastError = true)]
    private static partial int CryptUnprotectData(DATA_BLOB* input, char** description, DATA_BLOB* entropy, void* reserved, void* prompt, uint flags, DATA_BLOB* output);
}
