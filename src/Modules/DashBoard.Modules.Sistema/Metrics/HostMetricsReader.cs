using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DashBoard.Modules.Sistema.Metrics;

public sealed class HostMetricsReader(
    IOptions<SistemaOptions> options,
    ILogger<HostMetricsReader> logger,
    TimeProvider timeProvider)
{
    private readonly SistemaOptions _options = options.Value;
    private readonly HashSet<string> _failing = [];
    private CpuTimes? _previousCpu;

    public HostMetrics Read()
    {
        var cpu = Guard("cpu", ReadCpu);
        var memory = Guard("memoria", ReadMemory);
        var disk = Guard("disco", ReadDisk);
        var uptime = Guard("uptime", ReadUptime);
        var temperature = Guard("temperatura", ReadTemperature);

        return new HostMetrics(cpu, memory, disk, uptime, temperature, timeProvider.GetUtcNow());
    }

    private T? Guard<T>(string name, Func<T?> read)
    {
        try
        {
            var value = read();
            _failing.Remove(name);
            return value;
        }
        catch (Exception ex)
        {
            if (_failing.Add(name))
            {
                logger.LogWarning(ex, "No se pudo leer la métrica '{Metric}'", name);
            }

            return default;
        }
    }

    private double? ReadCpu()
    {
        var content = File.ReadAllText(Path.Combine(_options.ProcPath, "stat"));
        if (!CpuStatParser.TryParse(content, out var current))
        {
            throw new InvalidDataException("Formato de /proc/stat no válido");
        }

        var previous = _previousCpu;
        _previousCpu = current;
        return previous is { } p ? current.UsagePercentSince(p) : null;
    }

    private UsageBytes? ReadMemory()
    {
        var content = File.ReadAllText(Path.Combine(_options.ProcPath, "meminfo"));
        if (!MemInfoParser.TryParse(content, out var usage) || usage is null)
        {
            throw new InvalidDataException("Formato de /proc/meminfo no válido");
        }

        return usage;
    }

    private TimeSpan? ReadUptime()
    {
        var content = File.ReadAllText(Path.Combine(_options.ProcPath, "uptime"));
        if (!UptimeParser.TryParse(content, out var uptime))
        {
            throw new InvalidDataException("Formato de /proc/uptime no válido");
        }

        return uptime;
    }

    private UsageBytes? ReadDisk()
    {
        // En Windows DriveInfo resuelve cualquier ruta a su unidad, así que se comprueba antes.
        if (!Directory.Exists(_options.DiskPath))
        {
            throw new DirectoryNotFoundException($"No existe la ruta '{_options.DiskPath}'");
        }

        var drive = new DriveInfo(_options.DiskPath);
        return new UsageBytes(drive.TotalSize - drive.AvailableFreeSpace, drive.TotalSize);
    }

    private double? ReadTemperature()
    {
        var thermal = Path.Combine(_options.SysPath, "class", "thermal");
        double? max = null;

        foreach (var zone in Directory.EnumerateDirectories(thermal, "thermal_zone*"))
        {
            try
            {
                var content = File.ReadAllText(Path.Combine(zone, "temp"));
                if (ThermalParser.TryParse(content, out var celsius) && (max is null || celsius > max))
                {
                    max = celsius;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return max ?? throw new InvalidDataException("Ninguna zona térmica válida");
    }
}
