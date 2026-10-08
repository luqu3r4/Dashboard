using DashBoard.Modules.Salud.Charts;

namespace DashBoard.Modules.Salud.Tests;

public class ChartTicksTests
{
    [Fact]
    public void NiceScaleRoundsStepsMaxUpToARoundTick()
    {
        var axis = ChartGeometry.NiceAxis(new ChartScale(0, 9060));

        Assert.Equal(new ChartScale(0, 10000), axis.Scale);
        Assert.Equal([0, 2500, 5000, 7500, 10000], axis.Ticks);
    }

    [Fact]
    public void NiceScaleForWeightKeepsAZoomedRange()
    {
        // Pesos entre 86,4 y 89,3 (con el margen de LineScale): no debe empezar en 0.
        var axis = ChartGeometry.NiceAxis(new ChartScale(86.25, 89.45));

        Assert.Equal(new ChartScale(86, 90), axis.Scale);
        Assert.Equal([86, 87, 88, 89, 90], axis.Ticks);
    }

    [Fact]
    public void NiceScaleForSmallCounts()
    {
        var axis = ChartGeometry.NiceAxis(new ChartScale(0, 3));

        Assert.Equal([0, 1, 2, 3], axis.Ticks);
    }

    [Fact]
    public void NiceScaleForEmptyRangeDoesNotLoop()
    {
        var axis = ChartGeometry.NiceAxis(new ChartScale(0, 0));

        Assert.NotEmpty(axis.Ticks);
        Assert.True(axis.Scale.Max > axis.Scale.Min);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(500, "500")]
    [InlineData(2500, "2,5k")]
    [InlineData(10000, "10k")]
    [InlineData(87.5, "87,5")]
    public void CompactFormatsAxisLabels(double value, string expected)
    {
        Assert.Equal(expected, ChartGeometry.Compact(value));
    }
}
