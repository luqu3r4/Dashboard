using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashBoard.Modules.Salud;

public sealed class SaludModule : DashBoard.Core.IDashboardModule
{
    public string Title => "Salud";
    public string Description => "Pasos, peso y entrenamientos sincronizados desde el móvil.";
    public string Icon => "bi-heart-pulse-fill";
    public string Route => "salud";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SaludOptions>(configuration.GetSection(SaludOptions.SectionName));
    }
}
