using Pizza.Core.Conversions;

namespace Pizza.Core.Gesture;

/// <summary>Geometria das fatias (WF-1) e o que cada arquivo pode virar. Medidas no disco de 380 px.</summary>
public static class RadialMenu
{
    public const int SlotCount = 6;
    public const double InnerRadius = 92;
    public const double OuterRadius = 168;
    public const double HalfSliceDegrees = 27.5;

    private const double SlotDegrees = 360.0 / SlotCount;
    private const double TopDegrees = -90;

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

    /// <summary>
    /// Fatia sob o ponto como ela está desenhada agora: girada na troca de aba e, a que está em hover,
    /// maior e deslocada (WF-2). O alvo do drop precisa ser o que o usuário vê.
    /// </summary>
    public static int? SlotAt(double x, double y, double rotationDegrees, int? hoveredSlot, double hover)
    {
        var turn = -rotationDegrees * Math.PI / 180;
        var rx = (x * Math.Cos(turn)) - (y * Math.Sin(turn));
        var ry = (x * Math.Sin(turn)) + (y * Math.Cos(turn));

        if (hoveredSlot is { } slot && hover > 0)
        {
            // Mesmo desenho do Visual.SliceGeometry: 106% e 5 px para fora, na direção da fatia.
            var direction = SlotAngle(slot) * Math.PI / 180;
            var scale = 1 + (0.06 * hover);
            var hx = (rx - (5 * hover * Math.Cos(direction))) / scale;
            var hy = (ry - (5 * hover * Math.Sin(direction))) / scale;
            if (SlotAt(hx, hy) == slot)
            {
                return slot;
            }
        }

        return SlotAt(rx, ry);
    }

    /// <summary>Se existe alguma conversão para o arquivo; sem nenhuma, o menu não abre (FR-09).</summary>
    public static bool CanConvert(string path) => Available(path).Any();

    /// <summary>Fatias de uma aba para o arquivo, na ordem do catálogo. O que passar de seis fica de fora.</summary>
    public static IReadOnlyList<ConverterInfo> Slices(int tab, string path) =>
        Available(path).Where(converter => converter.Tab == tab).Take(SlotCount).ToArray();

    private static IEnumerable<ConverterInfo> Available(string path)
    {
        var extension = Path.GetExtension(path);
        return ConverterCatalog.All.Where(converter => converter.InputExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
    }
}
