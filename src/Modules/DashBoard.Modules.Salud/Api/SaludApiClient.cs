using System.Globalization;
using System.Net;
using System.Text.Json;

namespace DashBoard.Modules.Salud.Api;

/// <summary>Cliente de solo lectura de SaludApi. Nunca lanza por errores de red o de contenido: devuelve un estado.</summary>
public sealed class SaludApiClient(HttpClient http, SaludOptions options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SaludResult<IReadOnlyList<DailyValue>>> GetDailyAsync(string type, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var path = $"/api/health-records/daily?type={Uri.EscapeDataString(type)}&from={Format(from)}&to={Format(to)}";
        var result = await GetAsync<List<DailyDto>>(path, ct);
        if (result.Status != SaludStatus.Ok)
        {
            return SaludResult<IReadOnlyList<DailyValue>>.Fail(result.Status);
        }

        IReadOnlyList<DailyValue> data = [.. result.Data!.Select(d => new DailyValue(d.Date, d.Value, d.Count))];
        return SaludResult<IReadOnlyList<DailyValue>>.Ok(data);
    }

    public async Task<SaludResult<IReadOnlyList<Workout>>> GetWorkoutsAsync(DateTimeOffset fromUtc, CancellationToken ct)
    {
        var path = $"/api/health-records?type=ExerciseSession&from={Uri.EscapeDataString(fromUtc.ToString("O", CultureInfo.InvariantCulture))}";
        var result = await GetAsync<List<RecordDto>>(path, ct);
        if (result.Status != SaludStatus.Ok)
        {
            return SaludResult<IReadOnlyList<Workout>>.Fail(result.Status);
        }

        var workouts = new List<Workout>();
        foreach (var record in result.Data!)
        {
            if (record.EndTime is not { } end)
            {
                continue;
            }

            workouts.Add(new Workout(record.StartTime, ReadTitle(record.PayloadJson), (end - record.StartTime).TotalMinutes));
        }

        return SaludResult<IReadOnlyList<Workout>>.Ok(workouts);
    }

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string ReadTitle(string? payloadJson)
    {
        const string fallback = "Entrenamiento";
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return fallback;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("title", out var title)
                && title.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(title.GetString()))
            {
                return title.GetString()!;
            }
        }
        catch (JsonException)
        {
            // payload ilegible: se usa el título por defecto
        }

        return fallback;
    }

    private async Task<SaludResult<T>> GetAsync<T>(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.ApiUrl))
        {
            return SaludResult<T>.Fail(SaludStatus.NotConfigured);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (!string.IsNullOrEmpty(options.ApiKey))
            {
                request.Headers.Add("X-Api-Key", options.ApiKey);
            }

            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return SaludResult<T>.Fail(SaludStatus.Unauthorized);
            }

            if (!response.IsSuccessStatusCode)
            {
                return SaludResult<T>.Fail(SaludStatus.Unreachable);
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<T>(body, Json);
            return data is null || (data is System.Collections.IEnumerable items && items.Cast<object?>().Any(i => i is null))
                ? SaludResult<T>.Fail(SaludStatus.InvalidResponse)
                : SaludResult<T>.Ok(data);
        }
        catch (JsonException)
        {
            return SaludResult<T>.Fail(SaludStatus.InvalidResponse);
        }
        catch (HttpRequestException)
        {
            return SaludResult<T>.Fail(SaludStatus.Unreachable);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout del HttpClient (no cancelación del llamador)
            return SaludResult<T>.Fail(SaludStatus.Unreachable);
        }
    }

    private sealed record DailyDto(DateOnly Date, double Value, int Count);

    private sealed record RecordDto(DateTimeOffset StartTime, DateTimeOffset? EndTime, string? PayloadJson);
}
