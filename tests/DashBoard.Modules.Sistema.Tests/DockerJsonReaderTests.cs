using DashBoard.Modules.Sistema.Containers;
using DashBoard.Modules.Sistema.Metrics;

namespace DashBoard.Modules.Sistema.Tests;

public class DockerJsonReaderTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Lista_lee_los_tres_contenedores()
    {
        Assert.True(DockerJsonReader.TryReadList(Fixture("docker-list.json"), out var list));

        Assert.Equal(3, list.Count);
        Assert.Equal(new ContainerSummary("aaa111", "dashboard-dashboard-1", "dashboard-dashboard", "running"), list[0]);
        Assert.Equal("exited", list[1].State);
        Assert.Equal("restarting", list[2].State);
    }

    [Fact]
    public void Lista_sin_nombres_usa_el_id_corto()
    {
        var json = "[{\"Id\":\"0123456789abcdef\",\"Names\":[],\"Image\":\"x\",\"State\":\"running\"}]";

        Assert.True(DockerJsonReader.TryReadList(json, out var list));
        Assert.Equal("0123456789ab", list[0].Name);
    }

    [Fact]
    public void Lista_vacia_es_valida()
    {
        Assert.True(DockerJsonReader.TryReadList("[]", out var list));
        Assert.Empty(list);
    }

    [Fact]
    public void Detalle_lee_StartedAt_con_nanosegundos()
    {
        Assert.True(DockerJsonReader.TryReadStartedAt(Fixture("docker-inspect.json"), out var startedAt));
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 8, 15, 42, TimeSpan.Zero).AddTicks(1234567), startedAt);
    }

    [Fact]
    public void Stats_cgroup_v2()
    {
        Assert.True(DockerJsonReader.TryReadStats(Fixture("docker-stats-cgroupv2.json"), out var stats));

        Assert.Equal(2000000000UL, stats.CpuTotalUsage);
        Assert.Equal(100000000000UL, stats.SystemCpuUsage);
        Assert.Equal(4, stats.OnlineCpus);
        Assert.Equal(new UsageBytes(157286400, 8589934592), stats.Memory);
    }

    [Fact]
    public void Stats_cgroup_v1()
    {
        Assert.True(DockerJsonReader.TryReadStats(Fixture("docker-stats-cgroupv1.json"), out var stats));

        Assert.Equal(new UsageBytes(104857600, 2147483648), stats.Memory);
    }

    [Fact]
    public void Stats_cache_mayor_que_uso_da_cero()
    {
        var json = "{\"cpu_stats\":{\"cpu_usage\":{\"total_usage\":1},\"system_cpu_usage\":2,\"online_cpus\":4}," +
                   "\"memory_stats\":{\"usage\":1000,\"limit\":8000,\"stats\":{\"inactive_file\":5000}}}";

        Assert.True(DockerJsonReader.TryReadStats(json, out var stats));
        Assert.Equal(0, stats.Memory!.Used);
    }

    [Fact]
    public void Stats_sin_memoria_mantiene_cpu()
    {
        var json = "{\"cpu_stats\":{\"cpu_usage\":{\"total_usage\":1},\"system_cpu_usage\":2,\"online_cpus\":4},\"memory_stats\":{}}";

        Assert.True(DockerJsonReader.TryReadStats(json, out var stats));
        Assert.Null(stats.Memory);
        Assert.Equal(4, stats.OnlineCpus);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"cpu_stats\":{}}")]
    public void Stats_invalidas_devuelven_false(string json)
    {
        Assert.False(DockerJsonReader.TryReadStats(json, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("[{\"Id\":\"a\"}]")]
    public void Lista_invalida_devuelve_false(string json)
    {
        Assert.False(DockerJsonReader.TryReadList(json, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"State\":{\"StartedAt\":\"ayer\"}}")]
    public void Detalle_invalido_devuelve_false(string json)
    {
        Assert.False(DockerJsonReader.TryReadStartedAt(json, out _));
    }
}
