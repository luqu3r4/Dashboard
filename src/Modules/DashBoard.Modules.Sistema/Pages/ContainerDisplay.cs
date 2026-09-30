using DashBoard.Modules.Sistema.Containers;

namespace DashBoard.Modules.Sistema.Pages;

public static class ContainerDisplay
{
    public static (string Label, string CssClass) Badge(string state) => state switch
    {
        "running" => ("En marcha", "text-bg-success"),
        "exited" or "created" => ("Parado", "text-bg-secondary"),
        "restarting" => ("Reiniciando", "text-bg-warning"),
        "paused" => ("En pausa", "text-bg-warning"),
        _ => (state, "text-bg-danger"),
    };

    // Primero los que están en marcha, después el resto; cada grupo por nombre.
    public static IReadOnlyList<ContainerInfo> Sort(IEnumerable<ContainerInfo> containers) =>
        containers
            .OrderBy(c => c.State == "running" ? 0 : 1)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
