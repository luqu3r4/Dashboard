using DashBoard.Modules.Sistema.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DashBoard.Modules.Sistema;

public sealed class SistemaModule : DashBoard.Core.IDashboardModule
{
    public string Title => "Sistema / Homelab";
    public string Description => "CPU, memoria, disco, temperatura y tiempo encendida de la máquina.";
    public string Icon => "bi-hdd-network-fill";
    public string Route => "sistema";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SistemaOptions>(configuration.GetSection(SistemaOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<HostMetricsReader>();
        services.AddSingleton<HostMetricsSampler>();
        services.AddHostedService(sp => sp.GetRequiredService<HostMetricsSampler>());
    }
}
