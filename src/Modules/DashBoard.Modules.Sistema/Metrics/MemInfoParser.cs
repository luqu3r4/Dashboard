using System.Globalization;

namespace DashBoard.Modules.Sistema.Metrics;

public static class MemInfoParser
{
    public static bool TryParse(string content, out UsageBytes? usage)
    {
        usage = null;
        if (string.IsNullOrEmpty(content))
        {
            return false;
        }

        long? total = null;
        long? available = null;
        foreach (var line in content.Split('\n'))
        {
            if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
            {
                total = ParseKb(line);
            }
            else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
            {
                available = ParseKb(line);
            }
        }

        if (total is null || available is null || available > total)
        {
            return false;
        }

        usage = new UsageBytes((total.Value - available.Value) * 1024, total.Value * 1024);
        return true;
    }

    private static long? ParseKb(string line)
    {
        var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length >= 2
            && long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var kb)
            && kb <= long.MaxValue / 1024)
        {
            return kb;
        }

        return null;
    }
}
