using DashBoard.Modules.Sistema.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DashBoard.Modules.Sistema.Tests;

public class HostMetricsSamplerTests
{
    [Fact]
    public async Task Intervalo_negativo_no_detiene_el_arranque()
    {
        var options = Options.Create(new SistemaOptions
        {
            ProcPath = Path.Combine(Path.GetTempPath(), "no-existe-" + Guid.NewGuid().ToString("N")),
            SysPath = Path.Combine(Path.GetTempPath(), "no-existe-" + Guid.NewGuid().ToString("N")),
            DiskPath = Path.Combine(Path.GetTempPath(), "no-existe-" + Guid.NewGuid().ToString("N")),
            RefreshInterval = TimeSpan.FromSeconds(-1),
        });
        var reader = new HostMetricsReader(options, NullLogger<HostMetricsReader>.Instance, TimeProvider.System);
        var sampler = new HostMetricsSampler(reader, options);

        await sampler.StartAsync(CancellationToken.None);
        // Si el intervalo no válido llegara a PeriodicTimer, ExecuteAsync fallaría enseguida;
        // se espera un margen breve a que la tarea termine (con el arreglo sigue en ejecución).
        await Task.WhenAny(sampler.ExecuteTask!, Task.Delay(TimeSpan.FromMilliseconds(300)));
        Assert.False(sampler.ExecuteTask!.IsFaulted);
        await sampler.StopAsync(CancellationToken.None);

        Assert.NotNull(sampler.Current);
    }
}
