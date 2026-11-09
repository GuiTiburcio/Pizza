namespace Pizza.Core.Gesture;

/// <summary>Uma fatia do menu: o formato de saída e a conversão que ela dispara.</summary>
public sealed record MenuSlice(string Label, string Caption, string ConverterId, string Extension);

/// <summary>Geometria das fatias (WF-1) e o que cada arquivo pode virar. Medidas no disco de 380 px.</summary>
public static class RadialMenu
{
    public const int SlotCount = 6;
    public const double InnerRadius = 92;
    public const double OuterRadius = 168;
    public const double HalfSliceDegrees = 27.5;

    private const double SlotDegrees = 360.0 / SlotCount;
    private const double TopDegrees = -90;

    private static readonly string[] JpegExtensions = [".jpg", ".jpeg"];
    private static readonly MenuSlice[] ImageSlices = [new("PNG", "Sem perda", "image.png", "png")];

    /// <summary>Ângulo do centro de uma fatia, em graus, com a primeira no topo e as demais em sentido horário.</summary>
    public static double SlotAngle(int slot) => TopDegrees + (SlotDegrees * slot);

    /// <summary>Fatia sob um ponto relativo ao centro do disco, por ângulo e raio (FR-05). Nula nas folgas.</summary>
    public static int? SlotAt(double x, double y)
    {
        var radius = Math.Sqrt((x * x) + (y * y));
        if (radius < InnerRadius || radius > OuterRadius)
        {
            return null;
        }

        var fromTop = ((Math.Atan2(y, x) * 180 / Math.PI) - TopDegrees + 360) % 360;
        var slot = (int)Math.Round(fromTop / SlotDegrees) % SlotCount;
        var offset = Math.Abs(fromTop - (Math.Round(fromTop / SlotDegrees) * SlotDegrees));
        return offset <= HalfSliceDegrees ? slot : null;
    }

    /// <summary>Se existe alguma conversão para o arquivo; sem nenhuma, o menu não abre (FR-09).</summary>
    public static bool CanConvert(string path) =>
        JpegExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Fatias de uma aba para o arquivo. As conversões novas entram aqui, na ordem do ADR-012.</summary>
    public static IReadOnlyList<MenuSlice> Slices(int tab, string path) =>
        tab == 0 && CanConvert(path) ? ImageSlices : [];
}
