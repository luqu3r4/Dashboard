using System.Net;
using System.Text;
using DashBoard.Modules.Salud.Api;

namespace DashBoard.Modules.Salud.Tests;

public class SaludApiClientTests
{
    private static readonly SaludOptions Configured = new() { ApiUrl = "http://saludapi:8080", ApiKey = "clave-test" };

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static SaludApiClient Create(FakeSaludHandler handler, SaludOptions? options = null, TimeSpan? timeout = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://saludapi:8080") };
        if (timeout is not null)
        {
            http.Timeout = timeout.Value;
        }

        return new SaludApiClient(http, options ?? Configured);
    }

    [Fact]
    public async Task GetDailySendsTypeDatesAndApiKey()
    {
        var handler = new FakeSaludHandler(_ => Json("[]"));
        var client = Create(handler);

        await client.GetDailyAsync("Steps", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), CancellationToken.None);

        Assert.Equal(["/api/health-records/daily?type=Steps&from=2026-09-01&to=2026-09-30"], handler.Requests);
        Assert.Equal(["clave-test"], handler.Headers);
    }

    [Fact]
    public async Task GetDailyParsesValues()
    {
        var handler = new FakeSaludHandler(_ => Json("""[{"date":"2026-09-06","value":8432,"count":37}]"""));
        var client = Create(handler);

        var result = await client.GetDailyAsync("Steps", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), CancellationToken.None);

        Assert.Equal(SaludStatus.Ok, result.Status);
        Assert.Equal([new DailyValue(new DateOnly(2026, 9, 6), 8432, 37)], result.Data);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task NotConfiguredWhenApiUrlIsBlank(string? url)
    {
        var handler = new FakeSaludHandler(_ => Json("[]"));
        var client = new SaludApiClient(new HttpClient(handler), new SaludOptions { ApiUrl = url, ApiKey = "k" });

        var result = await client.GetDailyAsync("Steps", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), CancellationToken.None);

        Assert.Equal(SaludStatus.NotConfigured, result.Status);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task UnauthorizedOn401And403(HttpStatusCode code)
    {
        var client = Create(new FakeSaludHandler(_ => new HttpResponseMessage(code)));

        var result = await client.GetDailyAsync("Steps", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), CancellationToken.None);

        Assert.Equal(SaludStatus.Unauthorized, result.Status);
    }

    [Fact]
    public async Task UnreachableOnNetworkError()
    {
        var client = Create(new FakeSaludHandler(_ => throw new HttpRequestException("caído")));

        var result = await client.GetDailyAsync("Steps", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), CancellationToken.None);

        Assert.Equal(SaludStatus.Unreachable, result.Status);
    }

    [Fact]
    public async Task UnreachableOnServerError()
    {
        var client = Create(new FakeSaludHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var result = await client.GetDailyAsync("Steps", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), CancellationToken.None);

        Assert.Equal(SaludStatus.Unreachable, result.Status);
    }

    [Fact]
    public async Task UnreachableOnTimeout()
    {
        var handler = new FakeSaludHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return Json("[]");
        });
        var client = Create(handler, timeout: TimeSpan.FromMilliseconds(100));

        var result = await client.GetDailyAsync("Steps", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), CancellationToken.None);

        Assert.Equal(SaludStatus.Unreachable, result.Status);
    }

    [Fact]
    public async Task CallerCancellationIsPropagated()
    {
        var handler = new FakeSaludHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return Json("[]");
        });
        var client = Create(handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetDailyAsync("Steps", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), cts.Token));
    }

    [Fact]
    public async Task InvalidResponseOnMalformedJson()
    {
        var client = Create(new FakeSaludHandler(_ => Json("no json")));

        var result = await client.GetDailyAsync("Steps", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), CancellationToken.None);

        Assert.Equal(SaludStatus.InvalidResponse, result.Status);
    }

    [Fact]
    public async Task GetWorkoutsMapsTitleAndMinutes()
    {
        const string body = """
            [
              {"id":1,"recordType":"ExerciseSession","startTime":"2026-10-06T10:00:00+00:00","endTime":"2026-10-06T11:02:00+00:00","payloadJson":"{\"exerciseType\":70,\"title\":\"Pierna\"}"},
              {"id":2,"recordType":"ExerciseSession","startTime":"2026-10-07T10:00:00+00:00","endTime":"2026-10-07T10:30:00+00:00","payloadJson":"{\"exerciseType\":70}"},
              {"id":3,"recordType":"ExerciseSession","startTime":"2026-10-08T10:00:00+00:00","endTime":null,"payloadJson":"{\"title\":\"Sin fin\"}"}
            ]
            """;
        var handler = new FakeSaludHandler(_ => Json(body));
        var client = Create(handler);
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await client.GetWorkoutsAsync(from, CancellationToken.None);

        Assert.Equal(SaludStatus.Ok, result.Status);
        Assert.Equal(
        [
            new Workout(new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero), "Pierna", 62),
            new Workout(new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero), "Entrenamiento", 30),
        ], result.Data);
        Assert.Equal(
            [$"/api/health-records?type=ExerciseSession&from={Uri.EscapeDataString(from.ToString("O"))}"],
            handler.Requests);
        Assert.Equal(["clave-test"], handler.Headers);
    }
}
