namespace DashBoard.Modules.Sistema.Tests;

/// <summary>Reloj fijo para pruebas.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
