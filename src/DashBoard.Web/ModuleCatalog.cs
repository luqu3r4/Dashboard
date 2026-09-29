using System.Reflection;
using DashBoard.Core;

namespace DashBoard.Web;

public sealed class ModuleCatalog(IReadOnlyList<IDashboardModule> modules)
{
    public IReadOnlyList<Assembly> Assemblies { get; } =
        modules.Select(m => m.GetType().Assembly).Distinct().ToList();
}
