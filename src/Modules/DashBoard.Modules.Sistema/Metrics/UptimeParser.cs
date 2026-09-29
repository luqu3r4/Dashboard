using System.Globalization;

namespace DashBoard.Modules.Sistema.Metrics;

public static class UptimeParser
{
    public static bool TryParse(string content, out TimeSpan uptime)
    {
        uptime = default;
        var first = content?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (first is null
            || !double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            || double.IsNaN(seconds) || double.IsInfinity(seconds)
            || seconds < 0 || seconds > TimeSpan.MaxValue.TotalSeconds)
        {
            return false;
        }

        uptime = TimeSpan.FromSeconds(seconds);
        return true;
    }
}
