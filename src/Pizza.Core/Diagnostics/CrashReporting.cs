using System.Runtime.InteropServices;

namespace Pizza.Core.Diagnostics;

/// <summary>
/// PRIV-05 (ADR-032): um travamento não gera dump nem relatório do Windows, que levariam o documento, nomes de
/// arquivo e a cópia da tela para o disco ou para fora do PC.
/// </summary>
public static partial class CrashReporting
{
    private const uint FailCriticalErrors = 0x0001;
    private const uint NoGpFaultErrorBox = 0x0002;
    private const uint NoOpenFileErrorBox = 0x8000;

    /// <summary>
    /// O Windows não chama o WER para este processo: sem dump, mesmo com o LocalDumps ligado, e sem relatório.
    /// Os processos filhos herdam o modo. É a primeira coisa a rodar.
    /// </summary>
    public static void Suppress() => SetErrorMode(FailCriticalErrors | NoGpFaultErrorBox | NoOpenFileErrorBox);

    /// <summary>
    /// Segunda barreira: tira os executáveis do WER no registro do usuário, sem pedir administrador. Sozinha não basta,
    /// porque o LocalDumps ainda grava o dump. Uma falha aqui não impede o Pizza de abrir.
    /// </summary>
    public static void ExcludeFromWer(IEnumerable<string> executables)
    {
        foreach (var executable in executables)
        {
            _ = WerAddExcludedApplication(executable, 0);
        }
    }

    [LibraryImport("kernel32.dll")]
    private static partial uint SetErrorMode(uint mode);

    [LibraryImport("wer.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int WerAddExcludedApplication(string path, int allUsers);
}
