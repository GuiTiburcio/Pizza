using System.Runtime.InteropServices;

namespace Pizza.Worker;

/// <summary>ADR-035 / SEC-26: ativa a restrição antes de inicializar codecs de áudio.</summary>
internal static unsafe partial class AudioPolicy
{
    public static bool EnableMicrosoftOnly()
    {
        const int ProcessSignaturePolicy = 8;
        uint microsoftSignedOnly = 1;
        return SetProcessMitigationPolicy(ProcessSignaturePolicy, &microsoftSignedOnly, (nuint)sizeof(uint)) != 0;
    }

    [LibraryImport("kernel32.dll")]
    private static partial int SetProcessMitigationPolicy(int policy, void* buffer, nuint size);
}
