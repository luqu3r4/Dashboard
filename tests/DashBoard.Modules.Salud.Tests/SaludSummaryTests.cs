using DashBoard.Modules.Salud.Api;
using DashBoard.Modules.Salud.Summary;

namespace DashBoard.Modules.Salud.Tests;

public class SaludSummaryTests
{
    private static DailyValue D(string date, double value, int count = 1) =>
        new(DateOnly.Parse(date), value, count);

    [Fact]
    public void RangeFor30DaysIncludesToday()
    {
        var range = SaludSummary.Range(Period.Days30, new DateOnly(2026, 10, 6));

        Assert.Equal((new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 6)), range);
    }

    [Fact]
    public void RangeFor90DaysAndYear()
    {
        var today = new DateOnly(2026, 10, 6);

        Assert.Equal(today.AddDays(-89), SaludSummary.Range(Period.Days90, today).From);
        Assert.Equal(today.AddDays(-364), SaludSummary.Range(Period.Year, today).From);
    }

    [Fact]
    public void MondayOfSunday()
    {
        Assert.Equal(new DateOnly(2026, 10, 5), SaludSummary.MondayOf(new DateOnly(2026, 10, 11)));
        Assert.Equal(new DateOnly(2026, 10, 5), SaludSummary.MondayOf(new DateOnly(2026, 10, 5)));
    }

    [Fact]
    public void TodayComputesKmAndGoalPercent()
    {
        var today = new DateOnly(2026, 10, 6);
        var steps = new[] { D("2026-10-06", 7840), D("2026-10-05", 1000) };
        var distance = new[] { D("2026-10-06", 5900) };

        var stats = SaludSummary.Today(steps, distance, today, 10000);

        Assert.Equal(7840, stats.Steps);
        Assert.Equal(5.9, stats.Km, 6);
        Assert.Equal(78.4, stats.StepsGoalPercent!.Value, 6);
        Assert.Null(SaludSummary.Today(steps, distance, today, null).StepsGoalPercent);
    }

    [Fact]
    public void TodayWithoutDataIsZero()
    {
        var stats = SaludSummary.Today([], [], new DateOnly(2026, 10, 6), 10000);

        Assert.Equal(new TodayStats(0, 0, 0), stats);
    }

    [Fact]
    public void CurrentWeekCountsSessionsSinceMonday()
    {
        var sessions = new[]
        {
            D("2026-10-05", 40, 1), D("2026-10-06", 90, 2), D("2026-10-04", 30, 1),
        };

        Assert.Equal(new WeekWorkouts(3, 4), SaludSummary.CurrentWeek(sessions, new DateOnly(2026, 10, 6), 4));
    }

    [Fact]
    public void WeightUsesLatestAndChangeOver30Days()
    {
        var weights = new[] { D("2026-09-20", 86.8), D("2026-10-06", 86.4), D("2026-09-05", 87.2) };

        var stats = SaludSummary.Weight(weights, new DateOnly(2026, 10, 6), 82)!;

        Assert.Equal(86.4, stats.CurrentKg);
        Assert.Equal(new DateOnly(2026, 10, 6), stats.Date);
        Assert.Equal(-0.8, stats.ChangeOver30Days!.Value, 6);
        Assert.Equal(4.4, stats.DistanceToTarget!.Value, 6);
    }

    [Fact]
    public void WeightWithoutOldMeasurementHasNoChange()
    {
        var today = new DateOnly(2026, 10, 6);

        var stats = SaludSummary.Weight([D("2026-10-06", 86.4)], today, null)!;

        Assert.Null(stats.ChangeOver30Days);
        Assert.Null(stats.DistanceToTarget);
        Assert.Null(SaludSummary.Weight([], today, 82));
    }

    [Fact]
    public void StepsSeriesFillsMissingDaysWithZero()
    {
        var today = new DateOnly(2026, 10, 6);
        var (from, to) = SaludSummary.Range(Period.Days30, today);
        var steps = new[] { D("2026-10-01", 8000), D("2026-10-06", 5000) };
        var distance = new[] { D("2026-10-01", 6000) };

        var series = SaludSummary.StepsSeries(steps, distance, from, to, Period.Days30);

        Assert.Equal(30, series.Count);
        Assert.Equal(28, series.Count(p => p.Value == 0));
        Assert.Equal("07/09", series[0].Label);
        var point = series.Single(p => p.Value == 8000);
        Assert.Contains("8.000", point.Tooltip);
        Assert.Contains("6,0 km", point.Tooltip);
    }

    [Fact]
    public void StepsSeriesForYearIsWeeklyAverage()
    {
        var from = new DateOnly(2026, 9, 7);
        var to = new DateOnly(2026, 9, 20);
        var steps = new[] { D("2026-09-08", 7000), D("2026-09-10", 14000) };
        var distance = new[] { D("2026-09-08", 5000), D("2026-09-10", 10000) };

        var series = SaludSummary.StepsSeries(steps, distance, from, to, Period.Year);

        Assert.Equal(2, series.Count);
        Assert.Equal("07/09", series[0].Label);
        Assert.Equal(3000, series[0].Value);
        Assert.Contains("3.000", series[0].Tooltip);
        Assert.Contains("0,0", series[1].Tooltip);
        Assert.Equal(0, series[1].Value);
    }

    [Fact]
    public void StepsSeriesForYearDividesPartialWeeksByDaysInRange()
    {
        // Rango jueves 10/09 a domingo 13/09: 4 días en la semana del lunes 07/09.
        var series = SaludSummary.StepsSeries(
            [D("2026-09-10", 4000), D("2026-09-11", 4000)], [],
            new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 13), Period.Year);

        Assert.Single(series);
        Assert.Equal(2000, series[0].Value);
    }

    [Fact]
    public void WorkoutsPerWeekGroupsByMonday()
    {
        var sessions = new[] { D("2026-09-08", 40, 1), D("2026-09-13", 30, 2), D("2026-09-23", 20, 1) };

        var series = SaludSummary.WorkoutsPerWeek(sessions, new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 27));

        Assert.Equal([3, 0, 1], series.Select(p => p.Value).ToArray());
        Assert.Equal(["07/09", "14/09", "21/09"], series.Select(p => p.Label).ToArray());
    }

    [Fact]
    public void WeightSeriesOrdersByDate()
    {
        var series = SaludSummary.WeightSeries([D("2026-10-06", 86.4), D("2026-09-20", 86.8)]);

        Assert.Equal([86.8, 86.4], series.Select(p => p.Value).ToArray());
        Assert.Equal("20/09", series[0].Label);
        Assert.Contains("86,8 kg", series[0].Tooltip);
    }

    [Fact]
    public void EmptyInputsProduceEmptyOrZeroSeries()
    {
        var from = new DateOnly(2026, 9, 7);
        var to = new DateOnly(2026, 9, 9);

        Assert.Equal(3, SaludSummary.StepsSeries([], [], from, to, Period.Days30).Count);
        Assert.All(SaludSummary.StepsSeries([], [], from, to, Period.Days30), p => Assert.Equal(0, p.Value));
        Assert.Empty(SaludSummary.WeightSeries([]));
        Assert.All(SaludSummary.WorkoutsPerWeek([], from, to), p => Assert.Equal(0, p.Value));
        Assert.Empty(SaludSummary.Latest([]));
        Assert.Equal(new WeekWorkouts(0, null), SaludSummary.CurrentWeek([], from, null));
    }

    [Fact]
    public void LatestReturnsFiveMostRecentFirst()
    {
        var workouts = Enumerable.Range(1, 7)
            .Select(i => new Workout(new DateTimeOffset(2026, 10, i, 8, 0, 0, TimeSpan.Zero), $"W{i}", 30))
            .ToList();

        var latest = SaludSummary.Latest(workouts);

        Assert.Equal(["W7", "W6", "W5", "W4", "W3"], latest.Select(w => w.Title).ToArray());
    }
}
