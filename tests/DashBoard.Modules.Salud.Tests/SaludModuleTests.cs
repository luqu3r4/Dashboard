using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DashBoard.Modules.Salud.Tests;

public class SaludModuleTests
{
    [Fact]
    public void ExposesTitleRouteAndIcon()
    {
        var module = new SaludModule();

        Assert.Equal("Salud", module.Title);
        Assert.Equal("salud", module.Route);
        Assert.Equal("bi-heart-pulse-fill", module.Icon);
        Assert.Equal("Pasos, peso y entrenamientos sincronizados desde el móvil.", module.Description);
    }

    [Fact]
    public void BindsOptionsFromSaludSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Salud:ApiUrl"] = "http://salud-api:8080",
                ["Salud:StepsPerDay"] = "10000",
            })
            .Build();
        var services = new ServiceCollection();

        new SaludModule().ConfigureServices(services, configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SaludOptions>>().Value;
        Assert.Equal("http://salud-api:8080", options.ApiUrl);
        Assert.Equal(10000, options.StepsPerDay);
        Assert.Equal("Europe/Madrid", options.TimeZone);
    }
}
