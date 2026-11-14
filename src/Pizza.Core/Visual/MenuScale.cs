namespace Pizza.Core.Visual;

/// <summary>
/// ADR-037: o menu ocupa no máximo 1/15 da área útil do monitor, em pixels físicos, e tudo encolhe junto.
/// O fator do usuário vai de 50% a 100% desse teto e vale para todos os menus.
/// </summary>
public static class MenuScale
{
    public const double Min = 0.5;
    public const double Max = 1.0;
    public const int ScreenShare = 15;
    private const double WheelStep = 0.05;

    /// <summary>Pixels físicos por pixel de desenho (96 dpi) para a área útil e o fator do usuário.</summary>
    public static double Fit(int workWidth, int workHeight, double user)
    {
        var target = (double)workWidth * workHeight / ScreenShare;
        return Math.Sqrt(target / ((double)DiscLayout.Width * DiscLayout.Height)) * Clamp(user);
    }

    /// <summary>A mesma escala como dpi, que é como as janelas já convertem medidas.</summary>
    public static int EffectiveDpi(int workWidth, int workHeight, double user) =>
        Math.Max(1, (int)Math.Round(96 * Fit(workWidth, workHeight, user)));

    public static double Clamp(double user) => double.IsNaN(user) ? Max : Math.Clamp(user, Min, Max);

    /// <summary>Ctrl + roda: 5% por entalhe inteiro de 120.</summary>
    public static double Step(double user, int wheelDelta) => Clamp(user + (WheelStep * wheelDelta / 120.0));

    /// <summary>Puxar a borda: o fator acompanha a distância ao centro (ou o lado) desde o início do arraste.</summary>
    public static double Drag(double startUser, double startDistance, double distance) =>
        startDistance <= 0 ? Clamp(startUser) : Clamp(startUser * distance / startDistance);
}
