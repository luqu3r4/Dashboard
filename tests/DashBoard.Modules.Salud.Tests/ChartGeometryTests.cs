using DashBoard.Modules.Salud.Charts;

namespace DashBoard.Modules.Salud.Tests;

public class ChartGeometryTests
{
    [Fact]
    public void BarScaleStartsAtZeroAndIncludesGoal()
    {
        var scale = ChartGeometry.BarScale([5000, 8000], 10000);

        Assert.Equal(new ChartScale(0, 10000), scale);
    }

    [Fact]
    public void BarScaleWithAllZerosAvoidsDivisionByZero()
    {
        var scale = ChartGeometry.BarScale([0, 0], null);

        Assert.Equal(0, scale.Min);
        Assert.Equal(1, scale.Max);
    }

    [Fact]
    public void LineScaleSinglePoint()
    {
        var scale = ChartGeometry.LineScale([86.4], null);

        Assert.Equal(85.4, scale.Min, 6);
        Assert.Equal(87.4, scale.Max, 6);
    }

    [Fact]
    public void LineScaleEmpty()
    {
        Assert.Equal(new ChartScale(0, 1), ChartGeometry.LineScale([], null));
    }

    [Fact]
    public void LineScaleIncludesGoalWithMargin()
    {
        var scale = ChartGeometry.LineScale([86, 87], 82);

        Assert.True(scale.Min < 82);
        Assert.True(scale.Max > 87);
    }

    [Fact]
    public void YMapsMinToBottomAndMaxToTop()
    {
        var scale = new ChartScale(10, 20);

        Assert.Equal(100, ChartGeometry.Y(10, scale, 100), 6);
        Assert.Equal(0, ChartGeometry.Y(20, scale, 100), 6);
        Assert.Equal(50, ChartGeometry.Y(15, scale, 100), 6);
    }

    [Fact]
    public void XAndBarWidthSpreadEvenly()
    {
        var barWidth = ChartGeometry.BarWidth(4, 400);

        Assert.True(barWidth < 100);
        Assert.True(barWidth > 0);
        Assert.True(ChartGeometry.X(0, 4, 400) >= 0);
        Assert.True(ChartGeometry.X(3, 4, 400) + barWidth <= 400);
        Assert.True(ChartGeometry.X(1, 4, 400) > ChartGeometry.X(0, 4, 400));
    }
}
