namespace Pizza.Core.Gesture;

/// <summary>As abas do menu radial, na ordem da barra (Parte 2.2) e dos traços no miolo (ADR-008).</summary>
public static class MenuTabs
{
    public static IReadOnlyList<string> Names { get; } = ["Imagem", "PDF", "Documento", "Áudio"];
}
