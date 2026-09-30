using System.Net;
using DashBoard.Modules.Sistema.Containers;
using DashBoard.Modules.Sistema.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DashBoard.Modules.Sistema.Tests;

public sealed class ContainerMetricsReaderTests
{
    private const string ListPath = "/containers/json?all=true";
    private const string DetailPath = "/containers/aaa111/json";
    private const string StatsPath = "/containers/aaa111/stats?stream=false&one-shot=true";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T10:15:42Z");

    private static string Fixture(string name) => File.ReadAllText(Path.Combine("Fixtures", name));

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    private static ContainerMetricsReader CreateReader(FakeDockerHandler handler, string? url = "http://docker-proxy:2375", TimeSpan? timeout = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://docker-proxy:2375") };
        if (timeout is { } t)
        {
            http.Timeout = t;
        }

        var client = new DockerApiClient(http);
        return new ContainerMetricsReader(
            client,
            Options.Create(new SistemaOptions { DockerApiUrl = url }),
            NullLogger<ContainerMetricsReader>.Instance,
            new FixedTimeProvider(Now));
    }

    private static HttpResponseMessage Healthy(HttpRequestMessage r, string? detail = null) =>
        r.RequestUri!.PathAndQuery switch
        {
            ListPath => Ok(Fixture("docker-list.json")),
            DetailPath => Ok(detail ?? Fixture("docker-inspect.json")),
            StatsPath => Ok(Fixture("docker-stats-cgroupv2.json")),
            _ => Status(HttpStatusCode.NotFound),
        };

    [Fact]
    public async Task Todo_correcto_devuelve_la_lista_con_metricas()
    {
        var handler = new FakeDockerHandler(r => Healthy(r));
        var reader = CreateReader(handler);

        var snapshot = await reader.ReadAsync(CancellationToken.None);

        var containers = Assert.IsAssignableFrom<IReadOnlyList<ContainerInfo>>(snapshot.Containers);
        Assert.Equal(3, containers.Count);
        var running = containers.Single(c => c.Id == "aaa111");
        Assert.Equal(TimeSpan.FromHours(2).TotalSeconds, running.Uptime!.Value.TotalSeconds, 0);
        Assert.Equal(new UsageBytes(157286400, 8589934592), running.Memory);
        Assert.Null(running.CpuPercent);
        foreach (var other in containers.Where(c => c.Id != "aaa111"))
        {
            Assert.Null(other.Uptime);
            Assert.Null(other.CpuPercent);
            Assert.Null(other.Memory);
        }

        Assert.DoesNotContain(handler.Requests, p => p.Contains("bbb222") || p.Contains("ccc333"));
    }

    [Fact]
    public async Task Segunda_lectura_calcula_la_cpu()
    {
        var first = Fixture("docker-stats-cgroupv2.json");
        var second = first
            .Replace("2000000000", "3000000000")
            .Replace("100000000000", "110000000000");
        var statsCalls = 0;
        var handler = new FakeDockerHandler(r =>
            r.RequestUri!.PathAndQuery == StatsPath
                ? Ok(Interlocked.Increment(ref statsCalls) == 1 ? first : second)
                : Healthy(r));
        var reader = CreateReader(handler);

        await reader.ReadAsync(CancellationToken.None);
        var snapshot = await reader.ReadAsync(CancellationToken.None);

        Assert.Equal(40.0, snapshot.Containers!.Single(c => c.Id == "aaa111").CpuPercent!.Value, 6);
    }

    [Fact]
    public async Task Si_falla_la_lista_no_hay_contenedores()
    {
        var reader = CreateReader(new FakeDockerHandler(_ => Status(HttpStatusCode.InternalServerError)));

        var snapshot = await reader.ReadAsync(CancellationToken.None);

        Assert.Null(snapshot.Containers);
    }

    [Fact]
    public async Task Tras_caer_el_proxy_la_lista_vuelve()
    {
        var listCalls = 0;
        var handler = new FakeDockerHandler(r =>
            r.RequestUri!.PathAndQuery == ListPath && Interlocked.Increment(ref listCalls) == 1
                ? Status(HttpStatusCode.InternalServerError)
                : Healthy(r));
        var reader = CreateReader(handler);

        var down = await reader.ReadAsync(CancellationToken.None);
        var up = await reader.ReadAsync(CancellationToken.None);

        Assert.Null(down.Containers);
        Assert.Equal(3, up.Containers!.Count);
    }

    [Fact]
    public async Task Si_fallan_las_estadisticas_solo_ese_contenedor_pierde_metricas()
    {
        const string list = """
            [
              {"Id":"aaa111","Names":["/falla"],"Image":"a","State":"running"},
              {"Id":"ddd444","Names":["/sano"],"Image":"d","State":"running"}
            ]
            """;
        var handler = new FakeDockerHandler(r => r.RequestUri!.PathAndQuery switch
        {
            ListPath => Ok(list),
            StatsPath => Status(HttpStatusCode.NotFound),
            "/containers/ddd444/json" => Ok(Fixture("docker-inspect.json").Replace("aaa111", "ddd444")),
            "/containers/ddd444/stats?stream=false&one-shot=true" => Ok(Fixture("docker-stats-cgroupv2.json")),
            _ => Healthy(r),
        });
        var reader = CreateReader(handler);

        var snapshot = await reader.ReadAsync(CancellationToken.None);

        Assert.Equal(2, snapshot.Containers!.Count);
        var failing = snapshot.Containers.Single(c => c.Id == "aaa111");
        Assert.Null(failing.CpuPercent);
        Assert.Null(failing.Memory);
        Assert.NotNull(failing.Uptime);
        var healthy = snapshot.Containers.Single(c => c.Id == "ddd444");
        Assert.NotNull(healthy.Memory);
        Assert.NotNull(healthy.Uptime);
    }

    [Fact]
    public async Task Timeout_del_proxy_da_no_disponible()
    {
        var handler = new FakeDockerHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return Ok("[]");
        });
        var reader = CreateReader(handler, timeout: TimeSpan.FromMilliseconds(100));

        var snapshot = await reader.ReadAsync(CancellationToken.None);

        Assert.Null(snapshot.Containers);
    }

    [Fact]
    public async Task Sin_url_no_hace_peticiones()
    {
        var handler = new FakeDockerHandler(r => Healthy(r));
        var reader = CreateReader(handler, "  ");

        var snapshot = await reader.ReadAsync(CancellationToken.None);

        Assert.Null(snapshot.Containers);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("2026-09-30T11:00:00Z")] // posterior al reloj
    [InlineData("0001-01-01T00:00:00Z")] // nunca arrancado
    public async Task StartedAt_imposible_da_uptime_null(string startedAt)
    {
        var detail = Fixture("docker-inspect.json").Replace("2026-09-30T08:15:42.123456789Z", startedAt);
        var reader = CreateReader(new FakeDockerHandler(r => Healthy(r, detail)));

        var snapshot = await reader.ReadAsync(CancellationToken.None);

        Assert.Null(snapshot.Containers!.Single(c => c.Id == "aaa111").Uptime);
    }
}
