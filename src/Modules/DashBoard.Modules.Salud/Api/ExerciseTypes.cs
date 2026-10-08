namespace DashBoard.Modules.Salud.Api;

/// <summary>Códigos de tipo de ejercicio de Health Connect (ExerciseSessionRecord.EXERCISE_TYPE_*) y su nombre en español.</summary>
public static class ExerciseTypes
{
    public const int Walking = 79;

    private static readonly Dictionary<int, string> Names = new()
    {
        [2] = "Bádminton",
        [5] = "Baloncesto",
        [8] = "Bici",
        [9] = "Bici estática",
        [11] = "Boxeo",
        [13] = "Calistenia",
        [16] = "Baile",
        [25] = "Elíptica",
        [36] = "HIIT",
        [37] = "Senderismo",
        [44] = "Artes marciales",
        [48] = "Pilates",
        [54] = "Remo",
        [56] = "Carrera",
        [57] = "Cinta",
        [64] = "Fútbol",
        [68] = "Escaleras",
        [70] = "Fuerza",
        [71] = "Estiramientos",
        [73] = "Natación (aguas abiertas)",
        [74] = "Natación",
        [76] = "Tenis",
        [79] = "Caminar",
        [81] = "Halterofilia",
        [83] = "Yoga",
    };

    /// <summary>Nombre en español, o null si el código no está en la lista.</summary>
    public static string? Name(int exerciseType) => Names.GetValueOrDefault(exerciseType);
}
