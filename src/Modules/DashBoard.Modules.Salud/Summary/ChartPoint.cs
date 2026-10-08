namespace DashBoard.Modules.Salud.Summary;

/// <summary>Punto de una gráfica: etiqueta del eje, valor y texto del tooltip.</summary>
public sealed record ChartPoint(string Label, double Value, string Tooltip);

/// <summary>Pasos y kilómetros de hoy, con el porcentaje del objetivo de pasos si hay objetivo.</summary>
public sealed record TodayStats(double Steps, double Km, double? StepsGoalPercent);

/// <summary>Entrenamientos de la semana en curso y objetivo semanal (si lo hay).</summary>
public sealed record WeekWorkouts(int Count, int? Goal);

/// <summary>Si la variación del peso acerca (Good) o aleja (Bad) del objetivo; Neutral sin objetivo o sin cambio.</summary>
public enum WeightTrend { Neutral, Good, Bad }

/// <summary>Último peso, variación respecto a hace 30 días y distancia al peso objetivo.</summary>
public sealed record WeightStats(double CurrentKg, DateOnly Date, double? ChangeOver30Days, double? DistanceToTarget);
