using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashBoard.Modules.Sistema;

public sealed class SistemaModule : DashBoard.Core.IDashboardModule
{
    public string Title => "Sistema / Homelab";
    public string Description => "CPU, memoria, disco, temperatura y tiempo encendida de la máquina.";
    public string Icon => "bi-hdd-network-fill";
    public string Route => "sistema";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }
}
