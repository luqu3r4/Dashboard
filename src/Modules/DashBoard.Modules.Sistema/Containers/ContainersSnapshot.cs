using DashBoard.Modules.Sistema.Metrics;

namespace DashBoard.Modules.Sistema.Containers;

/// <summary>Lectura de los contenedores. <c>Containers</c> es null si no se pudo obtener la lista.</summary>
public sealed record ContainersSnapshot(IReadOnlyList<ContainerInfo>? Containers, DateTimeOffset Timestamp);

public sealed record ContainerInfo(
    string Id, string Name, string Image, string State,
    TimeSpan? Uptime, double? CpuPercent, UsageBytes? Memory);
