namespace DashBoard.Modules.Salud.Api;

/// <summary>Valor agregado de un día (por zona horaria de SaludApi) y número de registros que lo componen.</summary>
public sealed record DailyValue(DateOnly Date, double Value, int Count);
