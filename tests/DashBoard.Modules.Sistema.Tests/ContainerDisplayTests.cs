using DashBoard.Modules.Sistema.Containers;
using DashBoard.Modules.Sistema.Pages;

namespace DashBoard.Modules.Sistema.Tests;

public class ContainerDisplayTests
{
    [Theory]
    [InlineData("running", "En marcha", "text-bg-success")]
    [InlineData("exited", "Parado", "text-bg-secondary")]
    [InlineData("created", "Parado", "text-bg-secondary")]
    [InlineData("restarting", "Reiniciando", "text-bg-warning")]
    [InlineData("paused", "En pausa", "text-bg-warning")]
    [InlineData("dead", "dead", "text-bg-danger")]
    public void Badge_por_estado(string state, string label, string css)
    {
        Assert.Equal((label, css), ContainerDisplay.Badge(state));
    }

    [Fact]
    public void Sort_pone_primero_los_que_estan_en_marcha()
    {
        var entrada = new[]
        {
            Contenedor("zeta", "exited"),
            Contenedor("beta", "running"),
            Contenedor("alfa", "exited"),
            Contenedor("Gamma", "running"),
        };

        var orden = ContainerDisplay.Sort(entrada).Select(c => c.Name);

        Assert.Equal(new[] { "beta", "Gamma", "alfa", "zeta" }, orden);
    }

    private static ContainerInfo Contenedor(string name, string state) =>
        new(name, name, "imagen", state, null, null, null);
}
