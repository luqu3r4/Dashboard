namespace DashBoard.Modules.Sistema.Metrics;

public readonly record struct CpuTimes(ulong Total, ulong Idle)
{
    public double? UsagePercentSince(CpuTimes previous)
    {
        if (Total <= previous.Total)
        {
            return null;
        }

        var deltaTotal = (double)(Total - previous.Total);
        var deltaIdle = Idle >= previous.Idle ? (double)(Idle - previous.Idle) : 0.0;
        return (1 - deltaIdle / deltaTotal) * 100;
    }
}
