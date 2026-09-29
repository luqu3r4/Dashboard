using DashBoard.Modules.Sistema.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DashBoard.Modules.Sistema.Tests;

public sealed class HostMetricsReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sistema-tests-" + Guid.NewGuid().ToString("N"));
    private readonly CountingLogger _logger = new();

    public HostMetricsReaderTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "proc"));
        Write("proc/stat", "cpu  100 0 100 800 0 0 0 0 0 0\ncpu0 100 0 100 800 0 0 0 0 0 0\n");
        Write("proc/meminfo", "MemTotal:        1000 kB\nMemFree:          100 kB\nMemAvailable:     400 kB\n");
        Write("proc/uptime", "12345.67 54321.00\n");
        Write("sys/class/thermal/thermal_zone0/temp", "41000\n");
        Write("sys/class/thermal/thermal_zone1/temp", "52500\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private void Delete(string relative) =>
        File.Delete(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)));

    private HostMetricsReader CreateReader(string? diskPath = null) =>
        new(Options.Create(new SistemaOptions
        {
            ProcPath = Path.Combine(_root, "proc"),
            SysPath = Path.Combine(_root, "sys"),
            DiskPath = diskPath ?? _root,
        }), _logger, TimeProvider.System);

    [Fact]
    public void Lee_todas_las_metricas()
    {
        var m = CreateReader().Read();

        Assert.NotNull(m.Memory);
        Assert.NotNull(m.Uptime);
        Assert.NotNull(m.Disk);
        Assert.Equal(52.5, m.TemperatureCelsius);
        Assert.Null(m.CpuPercent);
    }

    [Fact]
    public void Segunda_lectura_calcula_cpu()
    {
        var reader = CreateReader();
        reader.Read();
        Write("proc/stat", "cpu  200 0 200 1600 0 0 0 0 0 0\n");

        var m = reader.Read();

        Assert.NotNull(m.CpuPercent);
        Assert.Equal(20.0, m.CpuPercent!.Value, 6);
    }

    [Fact]
    public void Zona_termica_rota_se_ignora()
    {
        Write("sys/class/thermal/thermal_zone1/temp", "basura");
        Directory.CreateDirectory(Path.Combine(_root, "sys", "class", "thermal", "thermal_zone2"));

        var m = CreateReader().Read();

        Assert.Equal(41.0, m.TemperatureCelsius);
    }

    [Fact]
    public void Fuente_rota_solo_anula_su_metrica()
    {
        Delete("proc/meminfo");

        var m = CreateReader().Read();

        Assert.Null(m.Memory);
        Assert.NotNull(m.Uptime);
        Assert.NotNull(m.TemperatureCelsius);
    }

    [Fact]
    public void Disco_inexistente_es_null()
    {
        var m = CreateReader(Path.Combine(_root, "no-existe")).Read();

        Assert.Null(m.Disk);
        Assert.NotNull(m.Memory);
        Assert.NotNull(m.Uptime);
        Assert.NotNull(m.TemperatureCelsius);
    }

    [Fact]
    public void Fallo_repetido_se_registra_una_vez()
    {
        Delete("proc/meminfo");
        var reader = CreateReader();

        reader.Read();
        reader.Read();
        reader.Read();
        Assert.Equal(1, _logger.Warnings);

        Write("proc/meminfo", "MemTotal:        1000 kB\nMemAvailable:     400 kB\n");
        reader.Read();
        Delete("proc/meminfo");
        reader.Read();

        Assert.Equal(2, _logger.Warnings);
    }

    private sealed class CountingLogger : ILogger<HostMetricsReader>
    {
        public int Warnings { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings++;
            }
        }
    }
}
