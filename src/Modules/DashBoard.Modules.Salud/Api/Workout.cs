namespace DashBoard.Modules.Salud.Api;

public sealed record Workout(DateTimeOffset Start, string Title, double Minutes);
