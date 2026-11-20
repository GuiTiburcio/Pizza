using System.Reflection;

namespace Pizza.App.Tests;

/// <summary>ADR-037: cada timer do menu tem o seu número; dois iguais se cancelam sem aviso.</summary>
public class OverlayTimerTests
{
    [Fact]
    public void Timers_do_menu_tem_numeros_distintos()
    {
        var timers = typeof(Overlay).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(nuint) && field.Name.StartsWith("Timer", StringComparison.Ordinal))
            .Select(field => Convert.ToUInt64(field.GetRawConstantValue()!))
            .ToList();

        Assert.Contains(9UL, timers);
        Assert.Equal(timers.Count, timers.Distinct().Count());
    }
}
