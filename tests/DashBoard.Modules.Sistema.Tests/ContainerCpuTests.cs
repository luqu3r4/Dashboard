using DashBoard.Modules.Sistema.Containers;

namespace DashBoard.Modules.Sistema.Tests;

public class ContainerCpuTests
{
    private static ContainerStats S(ulong total, ulong system, int cpus = 4) => new(total, system, cpus, null);

    [Fact]
    public void Dos_lecturas_dan_el_porcentaje()
    {
        var pct = ContainerCpu.Percent(S(1_000_000_000, 100_000_000_000), S(2_000_000_000, 110_000_000_000));
        Assert.NotNull(pct);
        Assert.Equal(40.0, pct.Value, 6);
    }

    [Fact]
    public void Sistema_sin_avance_da_null() =>
        Assert.Null(ContainerCpu.Percent(S(1, 100), S(2, 100)));

    [Fact]
    public void Contador_reiniciado_da_null() =>
        Assert.Null(ContainerCpu.Percent(S(5_000, 100), S(1_000, 200)));

    [Fact]
    public void Se_limita_a_100_por_cpu()
    {
        var pct = ContainerCpu.Percent(S(0, 0), S(20_000_000_000, 10_000_000_000));
        Assert.NotNull(pct);
        Assert.Equal(400.0, pct.Value, 6);
    }

    [Fact]
    public void Sin_uso_da_cero()
    {
        var pct = ContainerCpu.Percent(S(1_000, 100), S(1_000, 200));
        Assert.NotNull(pct);
        Assert.Equal(0.0, pct.Value, 6);
    }
}
