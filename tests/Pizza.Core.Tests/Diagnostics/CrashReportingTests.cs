using Microsoft.Win32;
using Pizza.Core.Diagnostics;

namespace Pizza.Core.Tests.Diagnostics;

/// <summary>PRIV-05: os executáveis do Pizza ficam fora do Relatório de Erros do Windows.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class CrashReportingTests
{
    private const string Excluded = @"Software\Microsoft\Windows\Windows Error Reporting\ExcludedApplications";

    [Fact]
    public void Exclusao_do_WER_fica_no_registro_do_usuario()
    {
        var name = $"Pizza.Teste-{Guid.NewGuid():N}.exe";
        var path = Path.Combine(Path.GetTempPath(), name);
        try
        {
            CrashReporting.ExcludeFromWer([path]);

            using var key = Registry.CurrentUser.OpenSubKey(Excluded);
            Assert.Equal(1, key?.GetValue(name));
        }
        finally
        {
            using var key = Registry.CurrentUser.OpenSubKey(Excluded, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
    }
}
