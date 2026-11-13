using Pizza.Core.Conversions;

namespace Pizza.Core.Gesture;

public readonly record struct MenuPage(int Tab, int Turn);

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

    /// <summary>Considera também as fatias que ainda estão voltando do hover, na ordem inversa do desenho.</summary>
    public static int? SlotAt(double x, double y, double rotationDegrees, IReadOnlyList<double> hovers)
    {
        for (var slot = hovers.Count - 1; slot >= 0; slot--)
        {
            if (hovers[slot] > 0 && SlotAt(x, y, rotationDegrees, slot, hovers[slot]) == slot)
            {
                return slot;
            }
        }

        // A área de repouso é uma tolerância para o destaque não oscilar quando a borda interna se afasta.
        return SlotAt(x, y, rotationDegrees, null, 0);
    }

    /// <summary>Se existe alguma conversão para o arquivo; sem nenhuma, o menu não abre (FR-09).</summary>
    public static bool CanConvert(string path) => Fitting(path).Any();

    /// <summary>Fatias de uma volta da aba, sem descartar os formatos das voltas seguintes (ADR-024).</summary>
    public static IReadOnlyList<ConverterInfo> Slices(int tab, string path, int turn = 0) =>
        Shown(path).Where(converter => converter.Tab == tab).Skip(turn * SlotCount).Take(SlotCount).ToArray();

    public static IReadOnlyList<MenuPage> Pages(string path) =>
        Enumerable.Range(0, MenuTabs.Names.Count)
            .SelectMany(tab => Enumerable.Range(0, Math.Max(1, (Shown(path).Count(converter => converter.Tab == tab) + SlotCount - 1) / SlotCount))
                .Select(turn => new MenuPage(tab, turn))).ToArray();

    /// <summary>FR-03: o menu abre na aba do tipo do arquivo, a primeira com alguma conversão para ele.</summary>
    public static int FirstPage(string path) => Pages(path).ToList().FindIndex(page => page.Tab == HomeTab(path));

    /// <summary>A primeira aba com alguma conversão para o arquivo.</summary>
    public static int HomeTab(string path) => Fitting(path).Select(converter => converter.Tab).DefaultIfEmpty(0).Min();

    /// <summary>Se a conversão aceita o tipo do arquivo.</summary>
    public static bool Fits(ConverterInfo converter, string path) =>
        converter.InputExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>ADR-036: o que a fatia apagada diz quando é escolhida.</summary>
    public static string Hint(string path) => $"Experimente a aba {MenuTabs.ShortNames[HomeTab(path)]}";

    /// <summary>ADR-034 e ADR-036: sem o motor de documentos (Pizza Brotinho), as fatias dele não existem.</summary>
    private static IEnumerable<ConverterInfo> Installed =>
        ConverterCatalog.All.Where(converter => !converter.DocumentEngine || ConverterCatalog.DocumentEngineAvailable);

    private static IEnumerable<ConverterInfo> Fitting(string path) => Installed.Where(converter => Fits(converter, path));

    /// <summary>
    /// ADR-036: numa aba com conversão para o arquivo, só as que servem a ele; nas outras, todas as fatias, que o
    /// menu desenha apagadas.
    /// </summary>
    private static IEnumerable<ConverterInfo> Shown(string path)
    {
        var home = Fitting(path).Select(converter => converter.Tab).ToHashSet();
        return Installed.Where(converter => !home.Contains(converter.Tab) || Fits(converter, path));
    }
}
