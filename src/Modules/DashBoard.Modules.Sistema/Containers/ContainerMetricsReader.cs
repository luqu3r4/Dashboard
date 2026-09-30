using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DashBoard.Modules.Sistema.Containers;

/// <summary>
/// Lee los contenedores de la API de Docker. Es con estado (recuerda la lectura anterior de CPU de
/// cada contenedor y los fallos ya registrados) y no es seguro entre hilos: solo debe llamarse
/// desde un único bucle.
/// </summary>
public sealed class ContainerMetricsReader(
    DockerApiClient client,
    IOptions<SistemaOptions> options,
    ILogger<ContainerMetricsReader> logger,
    TimeProvider timeProvider)
{
    private const string FailList = "lista";
    private const string FailDetail = "detalle";
    private const string FailStats = "estadisticas";

    private readonly SistemaOptions _options = options.Value;
    private readonly HashSet<string> _failing = [];
    private readonly Dictionary<string, ContainerStats> _previousStats = [];

    public async Task<ContainersSnapshot> ReadAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        if (string.IsNullOrWhiteSpace(_options.DockerApiUrl))
        {
            return new ContainersSnapshot(null, now);
        }

        var failures = new ConcurrentDictionary<string, Exception>();

        IReadOnlyList<ContainerSummary>? list = null;
        try
        {
            var json = await client.GetContainersAsync(ct);
            if (!DockerJsonReader.TryReadList(json, out var parsed))
            {
                throw new InvalidDataException("Formato de la lista de contenedores no válido");
            }

            list = parsed;
        }
        catch (Exception ex) when (!IsCancellation(ex, ct))
        {
            failures.TryAdd(FailList, ex);
        }

        if (list is null)
        {
            Report(failures, FailList);
            return new ContainersSnapshot(null, now);
        }

        var reads = await Task.WhenAll(list.Select(c => ReadContainerAsync(c, failures, ct)));

        var infos = new List<ContainerInfo>(list.Count);
        var kept = new HashSet<string>();
        for (var i = 0; i < list.Count; i++)
        {
            var summary = list[i];
            var (startedAt, stats) = reads[i];

            double? cpu = null;
            if (stats is { } current)
            {
                if (_previousStats.TryGetValue(summary.Id, out var previous))
                {
                    cpu = ContainerCpu.Percent(previous, current);
                }

                _previousStats[summary.Id] = current;
                kept.Add(summary.Id);
            }
            else if (summary.State == "running")
            {
                // Si las estadísticas fallaron solo este ciclo, se conserva la lectura anterior.
                kept.Add(summary.Id);
            }

            TimeSpan? uptime = null;
            if (startedAt is { } started && started.Year > 1 && now >= started)
            {
                uptime = now - started;
            }

            infos.Add(new ContainerInfo(summary.Id, summary.Name, summary.Image, summary.State,
                uptime, cpu, stats?.Memory));
        }

        // Se descartan las lecturas de contenedores que ya no existen o ya no están en marcha.
        foreach (var id in _previousStats.Keys.Where(id => !kept.Contains(id)).ToList())
        {
            _previousStats.Remove(id);
        }

        Report(failures, FailList, FailDetail, FailStats);
        return new ContainersSnapshot(infos, now);
    }

    private async Task<(DateTimeOffset? StartedAt, ContainerStats? Stats)> ReadContainerAsync(
        ContainerSummary container, ConcurrentDictionary<string, Exception> failures, CancellationToken ct)
    {
        if (container.State != "running")
        {
            return (null, null);
        }

        var detail = ReadDetailAsync(container.Id, failures, ct);
        var stats = ReadStatsAsync(container.Id, failures, ct);
        return (await detail, await stats);
    }

    private async Task<DateTimeOffset?> ReadDetailAsync(
        string id, ConcurrentDictionary<string, Exception> failures, CancellationToken ct)
    {
        try
        {
            var json = await client.GetContainerAsync(id, ct);
            return DockerJsonReader.TryReadStartedAt(json, out var startedAt)
                ? startedAt
                : throw new InvalidDataException($"Detalle del contenedor {id} no válido");
        }
        catch (Exception ex) when (!IsCancellation(ex, ct))
        {
            failures.TryAdd(FailDetail, ex);
            return null;
        }
    }

    private async Task<ContainerStats?> ReadStatsAsync(
        string id, ConcurrentDictionary<string, Exception> failures, CancellationToken ct)
    {
        try
        {
            var json = await client.GetStatsAsync(id, ct);
            return DockerJsonReader.TryReadStats(json, out var stats)
                ? stats
                : throw new InvalidDataException($"Estadísticas del contenedor {id} no válidas");
        }
        catch (Exception ex) when (!IsCancellation(ex, ct))
        {
            failures.TryAdd(FailStats, ex);
            return null;
        }
    }

    // Un timeout del HttpClient también es TaskCanceledException: solo cuenta como cancelación
    // si la pidió quien llama.
    private static bool IsCancellation(Exception ex, CancellationToken ct) =>
        ex is OperationCanceledException && ct.IsCancellationRequested;

    private void Report(ConcurrentDictionary<string, Exception> failures, params string[] kinds)
    {
        foreach (var kind in kinds)
        {
            if (!failures.TryGetValue(kind, out var ex))
            {
                _failing.Remove(kind);
            }
            else if (_failing.Add(kind))
            {
                logger.LogWarning(ex, "No se pudo leer de Docker: {Tipo}", kind);
            }
        }
    }
}
