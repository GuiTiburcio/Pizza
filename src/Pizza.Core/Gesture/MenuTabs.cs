namespace Pizza.Core.Gesture;

/// <summary>As abas do menu radial, na ordem da barra (Parte 2.2) e dos traços no miolo (ADR-008).</summary>
public static class MenuTabs
{
    public static IReadOnlyList<string> Names { get; } = ["Imagem", "PDF", "Documento", "Áudio"];

    /// <summary>
    /// Aba seguinte para um giro da roda do mouse. Rolar para baixo (delta negativo) avança,
    /// para cima volta; nas pontas, dá a volta.
    /// </summary>
    public static int Step(int current, int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return current;
        }

        var count = Names.Count;
        var next = wheelDelta < 0 ? current + 1 : current - 1;
        return ((next % count) + count) % count;
    }
}
