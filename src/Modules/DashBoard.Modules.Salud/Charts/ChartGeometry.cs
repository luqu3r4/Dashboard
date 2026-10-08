using System.Globalization;

namespace DashBoard.Modules.Salud.Charts;

/// <summary>Rango de valores representado en el eje vertical.</summary>
public sealed record ChartScale(double Min, double Max);

/// <summary>Escala ampliada a valores "redondos" y las marcas del eje Y que la dividen.</summary>
public sealed record ChartAxis(ChartScale Scale, IReadOnlyList<double> Ticks);

/// <summary>Cálculos puros de escala y posición para las gráficas SVG.</summary>
public static class ChartGeometry
{
    /// <summary>Pasos "redondos" (×10^n) entre los que se elige el intervalo del eje.</summary>
    private static readonly double[] NiceSteps = [1, 2, 2.5, 5, 10];

    /// <summary>
    /// Amplía <paramref name="scale"/> hasta marcas redondas (p. ej. 0–9.060 → 0–10.000 cada 2.500)
    /// para dibujar la cuadrícula; apunta a unas <paramref name="intervals"/> divisiones.
    /// </summary>
    public static ChartAxis NiceAxis(ChartScale scale, int intervals = 4)
    {
        var min = scale.Min;
        var max = scale.Max > scale.Min ? scale.Max : scale.Min + 1;

        var rough = (max - min) / intervals;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var step = NiceSteps.First(s => s * magnitude >= rough - 1e-12) * magnitude;

        var low = Math.Floor(min / step + 1e-9) * step;
        var high = Math.Ceiling(max / step - 1e-9) * step;
        var count = (int)Math.Round((high - low) / step);
        var ticks = Enumerable.Range(0, count + 1).Select(i => Math.Round(low + i * step, 10)).ToList();
        return new ChartAxis(new ChartScale(ticks[0], ticks[^1]), ticks);
    }

    /// <summary>Etiqueta corta del eje Y en es-ES: miles como "2,5k", el resto con un decimal como mucho.</summary>
    public static string Compact(double value) =>
        Math.Abs(value) >= 1000
            ? (value / 1000).ToString("0.#", CultureInfo.GetCultureInfo("es-ES")) + "k"
            : value.ToString("0.#", CultureInfo.GetCultureInfo("es-ES"));

    /// <summary>Proporción del hueco de cada barra que ocupa la barra.</summary>
    private const double BarFill = 0.7;

    /// <summary>Escala para barras: empieza en 0 y llega al mayor de valores y objetivo.</summary>
    public static ChartScale BarScale(IReadOnlyList<double> values, double? goal)
    {
        var max = values.Count > 0 ? values.Max() : 0;
        if (goal is { } g)
        {
            max = Math.Max(max, g);
        }

        return new ChartScale(0, max > 0 ? max : 1);
    }

    /// <summary>Escala para líneas: mínimo y máximo de valores y objetivo con un 5 % de margen.</summary>
    public static ChartScale LineScale(IReadOnlyList<double> values, double? goal)
    {
        var all = new List<double>(values);
        if (goal is { } g)
        {
            all.Add(g);
        }

        if (all.Count == 0)
        {
            return new ChartScale(0, 1);
        }

        var min = all.Min();
        var max = all.Max();
        if (max - min < 1e-9)
        {
            return new ChartScale(min - 1, max + 1);
        }

        var margin = (max - min) * 0.05;
        return new ChartScale(min - margin, max + margin);
    }

    /// <summary>Posición vertical: el mínimo abajo (<paramref name="height"/>) y el máximo arriba (0).</summary>
    public static double Y(double value, ChartScale scale, double height)
    {
        var range = scale.Max - scale.Min;
        if (range <= 0)
        {
            return height;
        }

        return height - (value - scale.Min) / range * height;
    }

    /// <summary>Ancho de cada barra dentro de su hueco.</summary>
    public static double BarWidth(int count, double width) =>
        count <= 0 ? 0 : width / count * BarFill;

    /// <summary>Posición horizontal (borde izquierdo) de la barra <paramref name="index"/>.</summary>
    public static double X(int index, int count, double width)
    {
        if (count <= 0)
        {
            return 0;
        }

        var slot = width / count;
        return index * slot + (slot - BarWidth(count, width)) / 2;
    }

    /// <summary>Índices que llevan etiqueta en el eje X (como mucho <paramref name="maxLabels"/>, repartidos).</summary>
    public static IReadOnlyList<int> LabelIndexes(int count, int maxLabels)
    {
        if (count <= 0 || maxLabels <= 0)
        {
            return [];
        }

        var step = (int)Math.Ceiling(count / (double)maxLabels);
        return Enumerable.Range(0, count).Where(i => i % step == 0).ToList();
    }

    /// <summary>Número en formato es-ES para las etiquetas del eje Y.</summary>
    public static string Format(double value) =>
        value.ToString(Math.Abs(value) >= 100 ? "N0" : "0.#", CultureInfo.GetCultureInfo("es-ES"));

    /// <summary>Número con punto decimal para atributos SVG.</summary>
    public static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
