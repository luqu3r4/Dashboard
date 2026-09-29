using System.Globalization;

namespace DashBoard.Modules.Sistema.Metrics;

public static class CpuStatParser
{
    public static bool TryParse(string content, out CpuTimes times)
    {
        times = default;
        if (string.IsNullOrEmpty(content))
        {
            return false;
        }

        foreach (var line in content.Split('\n'))
        {
            if (!line.StartsWith("cpu ", StringComparison.Ordinal))
            {
                continue;
            }

            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            // fields[0] = "cpu"; idle = fields[4], iowait = fields[5]
            if (fields.Length < 6)
            {
                return false;
            }

            ulong total = 0;
            var values = new ulong[fields.Length - 1];
            for (var i = 1; i < fields.Length; i++)
            {
                if (!ulong.TryParse(fields[i], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                {
                    return false;
                }

                values[i - 1] = value;
                // Solo los 8 primeros campos: guest y guest_nice ya van incluidos en user/nice (proc(5)).
                if (i <= 8)
                {
                    total += value;
                }
            }

            times = new CpuTimes(total, values[3] + values[4]);
            return true;
        }

        return false;
    }
}
