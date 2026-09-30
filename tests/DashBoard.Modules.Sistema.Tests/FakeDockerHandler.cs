namespace DashBoard.Modules.Sistema.Tests;

/// <summary>Responde a las peticiones con la función dada y registra las rutas pedidas.</summary>
internal sealed class FakeDockerHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(request.RequestUri!.PathAndQuery);
        }

        return Task.FromResult(respond(request));
    }
}
