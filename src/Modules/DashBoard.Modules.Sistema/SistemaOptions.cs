namespace DashBoard.Modules.Sistema;

public sealed class SistemaOptions
{
    public const string SectionName = "Sistema";

    public string ProcPath { get; set; } = "/proc";
    public string SysPath { get; set; } = "/sys";
    public string DiskPath { get; set; } = "/";

    /// <summary>URL de la API de Docker (proxy de solo lectura). Nulo o en blanco desactiva los contenedores.</summary>
    public string? DockerApiUrl { get; set; }

    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(2);
}
