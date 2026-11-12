using Pizza.Core.Conversions;
using Pizza.Core.Diagnostics;
using Pizza.Core.Gesture;

namespace Pizza.App;

// Substitui apenas os efeitos de borda: sem janela, captura de tela, log ou conversão.
// Overlay, DropTarget, Native e Pizza.Core são compilados diretamente dos arquivos de produção.
internal readonly record struct DiscFrame(
    double Scale, string? FileName, int FileCount, IReadOnlyList<ConverterInfo> Slices,
    double[] Hover, double TabPosition, int TargetTab, double Rotation,
    MotionStyle Motion, int? PulseSlot, int Turn = 0, int TurnCount = 1, bool Choosing = false,
    string? StripTitle = null, (string Label, string? Detail)[]? Levels = null, int Level = 0, int? LevelHover = null,
    double SliceGlide = 0, double SliceSwell = 0, double LevelGlide = 0, double LevelSwell = 0);

internal static class DiscRenderer
{
    public static void Prepare(int left, int top, int width, int height, int dpi) { }
    public static void Release() { }
    public static void Show(nint hwnd, int left, int top, DiscFrame frame) { }
}

internal static class Log
{
    public static void Write(LogEvent value, int? milliseconds = null) { }
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
