namespace DashBoard.Modules.Salud.Tests;

/// <summary>Responde a las peticiones con la función dada y registra las rutas pedidas y la cabecera X-Api-Key.</summary>
internal sealed class FakeSaludHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respondAsync) : HttpMessageHandler
{
    public FakeSaludHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : this((request, _) => Task.FromResult(respond(request)))
    {
    }

    public List<string> Requests { get; } = [];

    /// <summary>Valor de la cabecera X-Api-Key de cada petición (nulo si no se envió).</summary>
    public List<string?> Headers { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(request.RequestUri!.PathAndQuery);
            Headers.Add(request.Headers.TryGetValues("X-Api-Key", out var values) ? values.FirstOrDefault() : null);
        }

        return respondAsync(request, cancellationToken);
    }
}
