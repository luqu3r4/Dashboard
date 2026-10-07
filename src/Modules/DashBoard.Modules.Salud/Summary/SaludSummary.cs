using System.Globalization;
using DashBoard.Modules.Salud.Api;

namespace DashBoard.Modules.Salud.Summary;

/// <summary>Cálculos puros del resumen de Salud a partir de los valores diarios de SaludApi.</summary>
public static class SaludSummary
{
    private static readonly CultureInfo Es = new("es-ES");

    public static (DateOnly From, DateOnly To) Range(Period period, DateOnly today) => period switch
    {
        Period.Days30 => (today.AddDays(-29), today),
        Period.Days90 => (today.AddDays(-89), today),
        _ => (today.AddDays(-364), today),
    };

    public static DateOnly MondayOf(DateOnly day) =>
        day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    public static TodayStats Today(IReadOnlyList<DailyValue> steps, IReadOnlyList<DailyValue> distance, DateOnly today, int? stepsGoal)
    {
        var stepsToday = steps.Where(d => d.Date == today).Sum(d => d.Value);
        var km = distance.Where(d => d.Date == today).Sum(d => d.Value) / 1000;
        double? percent = stepsGoal is > 0 ? stepsToday * 100 / stepsGoal.Value : null;
        return new TodayStats(stepsToday, km, percent);
    }

    public static WeekWorkouts CurrentWeek(IReadOnlyList<DailyValue> sessions, DateOnly today, int? goal)
    {
        var monday = MondayOf(today);
        var count = sessions.Where(d => d.Date >= monday && d.Date <= today).Sum(d => d.Count);
        return new WeekWorkouts(count, goal);
    }

    public static WeightStats? Weight(IReadOnlyList<DailyValue> weights, DateOnly today, double? targetKg)
    {
        var current = weights.Where(d => d.Date <= today).OrderByDescending(d => d.Date).FirstOrDefault();
        if (current is null)
        {
            return null;
        }

        var reference = weights.Where(d => d.Date <= today.AddDays(-30) && d.Date < current.Date).OrderByDescending(d => d.Date).FirstOrDefault();
        double? change = reference is null ? null : Math.Round(current.Value - reference.Value, 2);
        double? distance = targetKg is null ? null : Math.Round(current.Value - targetKg.Value, 2);
        return new WeightStats(current.Value, current.Date, change, distance);
    }

    public static IReadOnlyList<ChartPoint> StepsSeries(
        IReadOnlyList<DailyValue> steps, IReadOnlyList<DailyValue> distance, DateOnly from, DateOnly to, Period period)
    {
        var stepsByDay = steps.GroupBy(d => d.Date).ToDictionary(g => g.Key, g => g.Sum(d => d.Value));
        var metersByDay = distance.GroupBy(d => d.Date).ToDictionary(g => g.Key, g => g.Sum(d => d.Value));
        var points = new List<ChartPoint>();

        if (period == Period.Year)
        {
            for (var monday = MondayOf(from); monday <= to; monday = monday.AddDays(7))
            {
                var first = monday < from ? from : monday;
                var last = monday.AddDays(6) > to ? to : monday.AddDays(6);
                var days = last.DayNumber - first.DayNumber + 1;
                double stepsSum = 0, metersSum = 0;
                for (var day = first; day <= last; day = day.AddDays(1))
                {
                    stepsSum += stepsByDay.GetValueOrDefault(day);
                    metersSum += metersByDay.GetValueOrDefault(day);
                }

                points.Add(Step(monday, stepsSum / days, metersSum / days, "media diaria, semana del "));
            }

            return points;
        }

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            points.Add(Step(day, stepsByDay.GetValueOrDefault(day), metersByDay.GetValueOrDefault(day), string.Empty));
        }

        return points;
    }

    public static IReadOnlyList<ChartPoint> WeightSeries(IReadOnlyList<DailyValue> weights) =>
        weights.OrderBy(d => d.Date)
            .Select(d => new ChartPoint(
                Label(d.Date),
                d.Value,
                $"{Label(d.Date)}: {d.Value.ToString("N1", Es)} kg"))
            .ToList();

    public static IReadOnlyList<ChartPoint> WorkoutsPerWeek(IReadOnlyList<DailyValue> sessions, DateOnly from, DateOnly to)
    {
        var points = new List<ChartPoint>();
        for (var monday = MondayOf(from); monday <= to; monday = monday.AddDays(7))
        {
            var sunday = monday.AddDays(6);
            var count = sessions.Where(d => d.Date >= monday && d.Date <= sunday).Sum(d => d.Count);
            points.Add(new ChartPoint(Label(monday), count, $"Semana del {Label(monday)}: {count}"));
        }

        return points;
    }

    public static IReadOnlyList<Workout> Latest(IReadOnlyList<Workout> workouts, int count = 5) =>
        workouts.OrderByDescending(w => w.Start).Take(count).ToList();

    private static ChartPoint Step(DateOnly date, double steps, double meters, string prefix) =>
        new(Label(date), steps,
            $"{prefix}{Label(date)}: {steps.ToString("N0", Es)} pasos, {(meters / 1000).ToString("N1", Es)} km");

    private static string Label(DateOnly date) => date.ToString("dd/MM", Es);
}
