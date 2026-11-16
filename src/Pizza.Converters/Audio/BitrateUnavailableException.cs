namespace Pizza.Converters.Audio;

/// <summary>Taxa menor aceita pelo Windows, ainda sem autorização para converter (ADR-035).</summary>
public sealed class BitrateUnavailableException(int suggested) : Exception
{
    public int Suggested { get; } = suggested;
}
