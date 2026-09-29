namespace DashBoard.Modules.Sistema.Metrics;

public sealed record UsageBytes(long Used, long Total)
{
    public double Percent => Total == 0 ? 0 : Used * 100.0 / Total;
}
