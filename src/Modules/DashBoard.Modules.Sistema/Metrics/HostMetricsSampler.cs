using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DashBoard.Modules.Sistema.Metrics;

/// <summary>
/// Lee las métricas de la máquina periódicamente en un único bucle y las publica
/// para que las consuman las páginas.
/// </summary>
public sealed class HostMetricsSampler(HostMetricsReader reader, IOptions<SistemaOptions> options) : BackgroundService
{
    private HostMetrics? _current;

    public HostMetrics? Current => Volatile.Read(ref _current);

    public event Action? Updated;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            Sample();

            using var timer = new PeriodicTimer(options.Value.RefreshInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                Sample();
            }
        }
        catch (OperationCanceledException)
        {
            // Parada normal.
        }
    }

    private void Sample()
    {
        try
        {
            Volatile.Write(ref _current, reader.Read());
        }
        catch (Exception)
        {
            // El lector ya aísla los fallos por métrica; esto evita que el bucle muera
            // ante una excepción inesperada. Se conserva la última lectura válida.
            return;
        }

        var handlers = Updated;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Action>())
        {
            try
            {
                handler();
            }
            catch (Exception)
            {
                // Un suscriptor defectuoso no debe detener el muestreo.
            }
        }
    }
}
