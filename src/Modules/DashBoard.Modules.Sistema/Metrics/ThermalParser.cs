using System.Globalization;

namespace DashBoard.Modules.Sistema.Metrics;

public static class ThermalParser
{
    public static bool TryParse(string content, out double celsius)
    {
        celsius = 0;
        if (content is null
            || !long.TryParse(content.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var milli))
        {
            return false;
        }

        celsius = milli / 1000.0;
        return true;
    }
}
