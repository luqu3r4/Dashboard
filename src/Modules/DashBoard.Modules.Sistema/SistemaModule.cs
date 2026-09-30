using DashBoard.Modules.Sistema.Containers;
using DashBoard.Modules.Sistema.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

        // Cliente propio (sin IHttpClientFactory): un singleton con conexiones que se
        // reciclan cada minuto y un timeout corto para que un proxy caído no bloquee el muestreo.
        services.AddSingleton(sp =>
        {
            var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(1) })
            {
                Timeout = TimeSpan.FromSeconds(2),
            };
            var url = sp.GetRequiredService<IOptions<SistemaOptions>>().Value.DockerApiUrl;
            if (!string.IsNullOrWhiteSpace(url))
            {
                http.BaseAddress = new Uri(url);
            }

            return new DockerApiClient(http);
        });
        services.AddSingleton<ContainerMetricsReader>();
        services.AddSingleton<ContainerMetricsSampler>();
        services.AddHostedService(sp => sp.GetRequiredService<ContainerMetricsSampler>());
    }
}
