namespace DashBoard.Modules.Sistema;

public sealed class SistemaOptions
{
    public const string SectionName = "Sistema";

    public string ProcPath { get; set; } = "/proc";
    public string SysPath { get; set; } = "/sys";
    public string DiskPath { get; set; } = "/";
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(2);
}
