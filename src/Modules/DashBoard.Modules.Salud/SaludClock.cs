namespace DashBoard.Modules.Salud;

public static class SaludClock
{
    /// <summary>Fecha de hoy en la zona horaria dada.</summary>
    public static DateOnly Today(TimeProvider time, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);
}
