namespace DashBoard.Modules.Sistema.Containers;

/// <summary>Cliente mínimo de la API de Docker: devuelve el JSON tal cual y lanza si el código no es de éxito.</summary>
public sealed class DockerApiClient(HttpClient http)
{
    public Task<string> GetContainersAsync(CancellationToken ct) =>
        GetAsync("/containers/json?all=true", ct);

    public Task<string> GetContainerAsync(string id, CancellationToken ct) =>
        GetAsync($"/containers/{Uri.EscapeDataString(id)}/json", ct);

    public Task<string> GetStatsAsync(string id, CancellationToken ct) =>
        GetAsync($"/containers/{Uri.EscapeDataString(id)}/stats?stream=false&one-shot=true", ct);

    private async Task<string> GetAsync(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
