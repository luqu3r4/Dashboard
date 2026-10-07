using Microsoft.Extensions.Configuration;
using DashBoard.Modules.Salud.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DashBoard.Modules.Salud;

/// <summary>Zona horaria del módulo; tipo propio para no registrar un <see cref="TimeZoneInfo"/> suelto en el contenedor compartido.</summary>
public sealed record SaludZone(TimeZoneInfo Zone);

public sealed class SaludModule : DashBoard.Core.IDashboardModule
{
    public string Title => "Salud";
    public string Description => "Pasos, peso y entrenamientos sincronizados desde el móvil.";
    public string Icon => "bi-heart-pulse-fill";
    public string Route => "salud";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SaludOptions>(configuration.GetSection(SaludOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(sp =>
        {
            var zone = sp.GetRequiredService<IOptions<SaludOptions>>().Value.TimeZone;
            return new SaludZone(TimeZoneInfo.FindSystemTimeZoneById(zone));
        });

        // Cliente propio (sin IHttpClientFactory), igual que el de Docker en el módulo Sistema.
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SaludOptions>>().Value;
            var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(1) })
            {
                Timeout = TimeSpan.FromSeconds(5),
            };
            if (!string.IsNullOrWhiteSpace(options.ApiUrl))
            {
                http.BaseAddress = new Uri(options.ApiUrl);
            }

            return new SaludApiClient(http, options);
        });
    }
}
