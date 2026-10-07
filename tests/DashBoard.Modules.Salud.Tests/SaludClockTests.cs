namespace DashBoard.Modules.Salud.Tests;

public class SaludClockTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public void UsesLocalDateAfterMidnight()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 9, 9, 22, 30, 0, TimeSpan.Zero));
        var madrid = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");

        Assert.Equal(new DateOnly(2026, 9, 10), SaludClock.Today(time, madrid));
    }
}
