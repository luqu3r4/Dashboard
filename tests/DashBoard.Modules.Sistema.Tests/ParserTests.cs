using System.Globalization;
using DashBoard.Modules.Sistema.Metrics;

namespace DashBoard.Modules.Sistema.Tests;

public class ParserTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void CpuStat_fixture_real()
    {
        var ok = CpuStatParser.TryParse(Fixture("stat.txt"), out var times);

        Assert.True(ok);
        // Suma a mano de los 8 primeros campos: 10132153 290696 3084719 46828483 16683 0 25195 0
        // (guest y guest_nice ya están incluidos en user/nice, ver proc(5); no se suman de nuevo)
        Assert.Equal(60377929UL, times.Total);
        // idle (46828483) + iowait (16683)
        Assert.Equal(46845166UL, times.Idle);
    }

    [Theory]
    [InlineData("")]
    [InlineData("intr 1 2 3")]
    public void CpuStat_texto_vacio_o_sin_linea_cpu(string content)
    {
        Assert.False(CpuStatParser.TryParse(content, out _));
    }

    [Fact]
    public void CpuUsage_entre_dos_lecturas()
    {
        var usage = new CpuTimes(200, 150).UsagePercentSince(new CpuTimes(100, 100));

        Assert.Equal(50.0, usage);
    }

    [Fact]
    public void CpuUsage_idle_decreciente_se_limita_a_100()
    {
        var usage = new CpuTimes(200, 40).UsagePercentSince(new CpuTimes(100, 50));

        Assert.Equal(100.0, usage);
    }

    [Fact]
    public void CpuUsage_idle_mayor_que_total_se_limita_a_0()
    {
        var usage = new CpuTimes(200, 250).UsagePercentSince(new CpuTimes(100, 100));

        Assert.Equal(0.0, usage);
    }

    [Fact]
    public void CpuUsage_sin_cambios_es_null()
    {
        var usage = new CpuTimes(100, 50).UsagePercentSince(new CpuTimes(100, 50));

        Assert.Null(usage);
    }

    [Fact]
    public void MemInfo_fixture_real()
    {
        var ok = MemInfoParser.TryParse(Fixture("meminfo.txt"), out var usage);

        Assert.True(ok);
        Assert.NotNull(usage);
        // MemTotal 16374128 kB * 1024
        Assert.Equal(16767107072L, usage.Total);
        // (16374128 - 12058732) kB * 1024
        Assert.Equal(4418965504L, usage.Used);
    }

    [Fact]
    public void MemInfo_sin_MemAvailable()
    {
        Assert.False(MemInfoParser.TryParse("MemTotal: 1000 kB\nMemFree: 500 kB", out _));
    }

    [Fact]
    public void Uptime_con_cultura_espanola()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");

            var ok = UptimeParser.TryParse("350735.47 234388.90", out var uptime);

            Assert.True(ok);
            Assert.Equal(TimeSpan.FromSeconds(350735.47), uptime);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Uptime_basura()
    {
        Assert.False(UptimeParser.TryParse("abc", out _));
    }

    [Fact]
    public void Thermal_miligrados()
    {
        Assert.True(ThermalParser.TryParse("45250\n", out var celsius));
        Assert.Equal(45.25, celsius);
    }

    [Theory]
    [InlineData("")]
    [InlineData("n/a")]
    public void Thermal_basura(string content)
    {
        Assert.False(ThermalParser.TryParse(content, out _));
    }
}
