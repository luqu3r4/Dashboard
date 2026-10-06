using System.Net;
using DashBoard.Modules.Sistema.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DashBoard.Modules.Sistema.Tests;

public sealed class ContainerMetricsSamplerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T10:15:42Z");

    private static string Fixture(string name) => File.ReadAllText(Path.Combine("Fixtures", name));

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static ContainerMetricsSampler CreateSampler(FakeDockerHandler handler, SistemaOptions sistemaOptions)
    {
        var options = Options.Create(sistemaOptions);
        var client = new DockerApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://docker-proxy:2375") });
        var reader = new ContainerMetricsReader(
            client,
            options,
            NullLogger<ContainerMetricsReader>.Instance,
            new FixedTimeProvider(Now));
        return new ContainerMetricsSampler(reader, options);
    }

    [Fact]
    public async Task Intervalo_negativo_no_detiene_el_arranque()
    {
        var handler = new FakeDockerHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var sampler = CreateSampler(handler, new SistemaOptions
        {
            DockerApiUrl = null,
            RefreshInterval = TimeSpan.FromSeconds(-1),
        });

        await sampler.StartAsync(CancellationToken.None);
        // Si el intervalo no válido llegara a PeriodicTimer, ExecuteAsync fallaría enseguida;
        // se espera un margen breve a que la tarea termine (con el arreglo sigue en ejecución).
        await Task.WhenAny(sampler.ExecuteTask!, Task.Delay(TimeSpan.FromMilliseconds(300)));
        Assert.False(sampler.ExecuteTask!.IsFaulted);
        await sampler.StopAsync(CancellationToken.None);

        Assert.NotNull(sampler.Current);
        Assert.Null(sampler.Current.Containers);
    }

    [Fact]
    public async Task Publica_la_lista_y_avisa_a_los_suscriptores()
    {
        var handler = new FakeDockerHandler(r => r.RequestUri!.PathAndQuery switch
        {
            "/containers/json?all=true" => Ok(Fixture("docker-list.json")),
            "/containers/aaa111/json" => Ok(Fixture("docker-inspect.json")),
            "/containers/aaa111/stats?stream=false&one-shot=true" => Ok(Fixture("docker-stats-cgroupv2.json")),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var sampler = CreateSampler(handler, new SistemaOptions
        {
            DockerApiUrl = "http://docker-proxy:2375",
            RefreshInterval = TimeSpan.FromSeconds(30),
        });
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sampler.Updated += () => throw new InvalidOperationException("suscriptor defectuoso");
        sampler.Updated += () => notified.TrySetResult();

        await sampler.StartAsync(CancellationToken.None);
        try
        {
            await notified.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(3, sampler.Current!.Containers!.Count);
        }
        finally
        {
            await sampler.StopAsync(CancellationToken.None);
        }
    }
}
