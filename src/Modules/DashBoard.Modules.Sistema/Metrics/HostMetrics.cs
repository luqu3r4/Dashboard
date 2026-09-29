namespace DashBoard.Modules.Sistema.Metrics;

public sealed record HostMetrics(
    double? CpuPercent,
    UsageBytes? Memory,
    UsageBytes? Disk,
    TimeSpan? Uptime,
    double? TemperatureCelsius,
    DateTimeOffset Timestamp);
