using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DashBoard.Modules.Sistema.Containers;

/// <summary>
/// Lee los contenedores Docker periódicamente en un único bucle y publica la última
/// lectura para que la consuman las páginas.
/// </summary>
public sealed class ContainerMetricsSampler(ContainerMetricsReader reader, IOptions<SistemaOptions> options) : BackgroundService
{
    private ContainersSnapshot? _current;

    public ContainersSnapshot? Current => Volatile.Read(ref _current);

    public event Action? Updated;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await SampleAsync(stoppingToken);

            // Un intervalo <= 0 es una configuración inválida: se usa el valor por defecto
            // en lugar de dejar que PeriodicTimer lance y detenga el host.
            var interval = options.Value.RefreshInterval;
            if (interval <= TimeSpan.Zero)
            {
                interval = TimeSpan.FromSeconds(2);
            }

            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SampleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Parada normal.
        }
    }

    private async Task SampleAsync(CancellationToken stoppingToken)
    {
        try
        {
            Volatile.Write(ref _current, await reader.ReadAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // El lector ya aísla los fallos de la API; esto evita que el bucle muera
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
