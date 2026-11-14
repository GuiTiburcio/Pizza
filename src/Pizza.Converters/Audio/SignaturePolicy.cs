using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Pizza.Converters.Audio;

/// <summary>
/// SEC-26 (ADR-035): antes do Media Foundation carregar os codecs, o Worker passa a aceitar só binários assinados pela
/// Microsoft. Um codec de terceiro instalado no PC não entra no processo que lê o arquivo não confiável. Vale para o
/// resto da vida do Worker, que converte um arquivo só (ADR-019).
/// </summary>
public static partial class SignaturePolicy
{
    private const int ProcessSignaturePolicy = 8;
    private const uint MicrosoftSignedOnly = 1;

    public static void RequireMicrosoftSigned()
    {
        var flags = MicrosoftSignedOnly;
        if (!SetProcessMitigationPolicy(ProcessSignaturePolicy, ref flags, sizeof(uint)))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    /// <summary>Se a política está ligada neste processo.</summary>
    public static bool IsMicrosoftSignedOnly()
    {
        uint flags = 0;
        return GetProcessMitigationPolicy(GetCurrentProcess(), ProcessSignaturePolicy, ref flags, sizeof(uint)) && (flags & MicrosoftSignedOnly) != 0;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessMitigationPolicy(int policy, ref uint buffer, nuint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessMitigationPolicy(nint process, int policy, ref uint buffer, nuint length);

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();
}
