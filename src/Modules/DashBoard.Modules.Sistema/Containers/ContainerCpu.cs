namespace DashBoard.Modules.Sistema.Containers;

/// <summary>Cálculo del uso de CPU de un contenedor a partir de dos lecturas consecutivas.</summary>
public static class ContainerCpu
{
    /// <summary>
    /// cpu% = (Δuso_total / Δuso_cpu_sistema) × cpus_online × 100, limitado a [0, 100 × cpus_online].
    /// Devuelve null si el sistema no avanzó o el contador del contenedor retrocedió (reinicio).
    /// </summary>
    public static double? Percent(ContainerStats previous, ContainerStats current)
    {
        // Los contadores son ulong: se comprueba el orden antes de restar.
        if (current.SystemCpuUsage <= previous.SystemCpuUsage) return null;
        if (current.CpuTotalUsage < previous.CpuTotalUsage) return null;

        double deltaTotal = current.CpuTotalUsage - previous.CpuTotalUsage;
        double deltaSystem = current.SystemCpuUsage - previous.SystemCpuUsage;
        double max = 100.0 * current.OnlineCpus;

        return Math.Clamp(deltaTotal / deltaSystem * max, 0, max);
    }
}
