using DashBoard.Modules.Salud.Api;

namespace DashBoard.Modules.Salud.Pages;

/// <summary>Textos de estado de la página y elección del mensaje cuando fallan varias llamadas.</summary>
public static class SaludMessages
{
    public const string NotConfigured = "El módulo Salud no está configurado (falta `Salud__ApiUrl`).";
    public const string Unreachable = "No se puede conectar con SaludApi.";
    public const string Unauthorized = "SaludApi ha rechazado la clave (revisa `Salud__ApiKey`).";
    public const string NoData = "Sin datos";

    /// <summary>
    /// Mensaje a mostrar para un conjunto de estados, o nulo si todos son correctos.
    /// Prioridad: no configurado, inaccesible (incluye respuesta inválida), clave rechazada.
    /// </summary>
    public static string? For(IEnumerable<SaludStatus> statuses)
    {
        var set = statuses.ToHashSet();
        if (set.Contains(SaludStatus.NotConfigured))
        {
            return NotConfigured;
        }

        if (set.Contains(SaludStatus.Unreachable) || set.Contains(SaludStatus.InvalidResponse))
        {
            return Unreachable;
        }

        return set.Contains(SaludStatus.Unauthorized) ? Unauthorized : null;
    }
}
