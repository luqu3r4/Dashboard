using System.Globalization;

namespace DashBoard.Modules.Sistema.Pages;

public static class MetricsFormat
{
    private const double BytesPerGigabyte = 1024d * 1024d * 1024d;

    public static string Uptime(TimeSpan uptime)
    {
        if (uptime.Days >= 1)
        {
            return $"{uptime.Days} d {uptime.Hours} h {uptime.Minutes} min";
        }

        if (uptime.Hours >= 1)
        {
            return $"{uptime.Hours} h {uptime.Minutes} min";
        }

        return $"{uptime.Minutes} min";
    }

    public static string Gigabytes(long bytes) =>
        string.Create(CultureInfo.CurrentCulture, $"{bytes / BytesPerGigabyte:0.0} GB");

    public static string TemperatureClass(double celsius) => celsius switch
    {
        >= 85 => "text-danger",
        >= 70 => "text-warning",
        _ => "",
    };
}
