using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashBoard.Core;

public interface IDashboardModule
{
    string Title { get; }        // "Sistema / Homelab"
    string Description { get; }  // texto de la tarjeta en Inicio
    string Icon { get; }         // clase de Bootstrap Icons, p. ej. "bi-hdd-network-fill"
    string Route { get; }        // ruta relativa sin barra inicial, p. ej. "sistema"
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
