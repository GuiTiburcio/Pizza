using Pizza.Core.Conversions;
using Pizza.Core.Diagnostics;

namespace Pizza.App;

// Sem janela, captura de tela, log ou conversão nos testes. O renderer real é exercitado
// em bitmap sintético; nunca se chama Prepare, que lê o fundo da tela.

internal static class Log
{
    public static void Write(LogEvent value) { }
}

internal static class OptionsPanel
{
    public static void TakeFocus(nint window) { }
    public static void Close() { }
    public static void Show(ConverterInfo converter, string fileName, int centerX, int centerY) =>
        throw new InvalidOperationException("O teste não deve abrir um painel.");
}

internal static class Notifications
{
    public static void Convert(string path, ConverterInfo converter, IReadOnlyDictionary<string, string> options) =>
        throw new InvalidOperationException("O teste não deve iniciar uma conversão.");
}
