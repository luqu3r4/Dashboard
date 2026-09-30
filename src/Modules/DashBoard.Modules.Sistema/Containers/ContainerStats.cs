using DashBoard.Modules.Sistema.Metrics;

namespace DashBoard.Modules.Sistema.Containers;

public readonly record struct ContainerStats(ulong CpuTotalUsage, ulong SystemCpuUsage, int OnlineCpus, UsageBytes? Memory);
