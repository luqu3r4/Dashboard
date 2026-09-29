using System.Globalization;
using DashBoard.Modules.Sistema.Pages;

namespace DashBoard.Modules.Sistema.Tests;

public class MetricsFormatTests
{
    [Theory]
    [InlineData(3, 4, 12, "3 d 4 h 12 min")]
    [InlineData(0, 2, 0, "2 h 0 min")]
    [InlineData(0, 0, 5, "5 min")]
    [InlineData(0, 0, 0, "0 min")]
    public void Uptime_formatos(int days, int hours, int minutes, string esperado)
    {
        Assert.Equal(esperado, MetricsFormat.Uptime(new TimeSpan(days, hours, minutes, 0)));
    }

    [Fact]
    public void Gigabytes_una_decimal()
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");
            Assert.Equal("1,5 GB", MetricsFormat.Gigabytes(1610612736));
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Theory]
    [InlineData(69.9, "")]
    [InlineData(70, "text-warning")]
    [InlineData(84.9, "text-warning")]
    [InlineData(85, "text-danger")]
    public void TemperatureClass_umbrales(double celsius, string esperado)
    {
        Assert.Equal(esperado, MetricsFormat.TemperatureClass(celsius));
    }
}
