namespace DashBoard.Modules.Salud;

public sealed class SaludOptions
{
    public const string SectionName = "Salud";

    /// <summary>URL de SaludApi. Nulo o en blanco indica que el módulo no está configurado.</summary>
    public string? ApiUrl { get; set; }

    /// <summary>Clave enviada a SaludApi en la cabecera X-Api-Key.</summary>
    public string? ApiKey { get; set; }

    public string TimeZone { get; set; } = "Europe/Madrid";

    /// <summary>Objetivo de pasos diarios (opcional).</summary>
    public int? StepsPerDay { get; set; }

    /// <summary>Objetivo de entrenamientos por semana (opcional).</summary>
    public int? WorkoutsPerWeek { get; set; }

    /// <summary>Peso objetivo en kg (opcional).</summary>
    public double? TargetWeightKg { get; set; }
}
