using System.Globalization;
using System.Text.Json;
using DashBoard.Modules.Sistema.Metrics;

namespace DashBoard.Modules.Sistema.Containers;

/// <summary>Lee los JSON de la API de Docker sin lanzar excepciones: ante datos inválidos devuelve false.</summary>
public static class DockerJsonReader
{
    public static bool TryReadList(string json, out IReadOnlyList<ContainerSummary> containers)
    {
        containers = [];
        if (!TryParse(json, out var doc))
        {
            return false;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var result = new List<ContainerSummary>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !TryGetString(item, "Id", out var id)
                    || !TryGetString(item, "Image", out var image)
                    || !TryGetString(item, "State", out var state))
                {
                    return false;
                }

                result.Add(new ContainerSummary(id, ReadName(item, id), image, state));
            }

            containers = result;
            return true;
        }
    }

    public static bool TryReadStartedAt(string json, out DateTimeOffset startedAt)
    {
        startedAt = default;
        if (!TryParse(json, out var doc))
        {
            return false;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("State", out var state)
                || state.ValueKind != JsonValueKind.Object
                || !TryGetString(state, "StartedAt", out var text))
            {
                return false;
            }

            // DateTimeOffset.Parse rechaza más de 7 decimales y Docker devuelve 9 (nanosegundos).
            return DateTimeOffset.TryParse(
                TruncateFraction(text, 7),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out startedAt);
        }
    }

    public static bool TryReadStats(string json, out ContainerStats stats)
    {
        stats = default;
        if (!TryParse(json, out var doc))
        {
            return false;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("cpu_stats", out var cpu)
                || cpu.ValueKind != JsonValueKind.Object
                || !cpu.TryGetProperty("cpu_usage", out var cpuUsage)
                || cpuUsage.ValueKind != JsonValueKind.Object
                || !TryGetUInt64(cpuUsage, "total_usage", out var total)
                || !TryGetUInt64(cpu, "system_cpu_usage", out var system)
                || !TryGetInt64(cpu, "online_cpus", out var online)
                || online <= 0
                || online > int.MaxValue)
            {
                return false;
            }

            stats = new ContainerStats(total, system, (int)online, ReadMemory(root));
            return true;
        }
    }

    private static UsageBytes? ReadMemory(JsonElement root)
    {
        if (!root.TryGetProperty("memory_stats", out var memory)
            || memory.ValueKind != JsonValueKind.Object
            || !TryGetInt64(memory, "usage", out var usage)
            || !TryGetInt64(memory, "limit", out var limit))
        {
            return null;
        }

        // cgroup v2 informa inactive_file y cgroup v1 total_inactive_file: es caché recuperable, no uso real.
        long cache = 0;
        if (memory.TryGetProperty("stats", out var details) && details.ValueKind == JsonValueKind.Object)
        {
            if (!TryGetInt64(details, "inactive_file", out cache))
            {
                TryGetInt64(details, "total_inactive_file", out cache);
            }
        }

        return new UsageBytes(Math.Max(0, usage - cache), limit);
    }

    private static string ReadName(JsonElement item, string id)
    {
        if (item.TryGetProperty("Names", out var names)
            && names.ValueKind == JsonValueKind.Array
            && names.GetArrayLength() > 0
            && names[0].ValueKind == JsonValueKind.String)
        {
            var name = names[0].GetString()!.TrimStart('/');
            if (name.Length > 0)
            {
                return name;
            }
        }

        return id.Length <= 12 ? id : id[..12];
    }

    private static string TruncateFraction(string text, int digits)
    {
        var dot = text.IndexOf('.');
        if (dot < 0)
        {
            return text;
        }

        var end = dot + 1;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        return end - dot - 1 <= digits ? text : text[..(dot + 1 + digits)] + text[end..];
    }

    private static bool TryParse(string json, out JsonDocument doc)
    {
        doc = null!;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            doc = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        value = "";
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString()!;
            return true;
        }

        return false;
    }

    private static bool TryGetUInt64(JsonElement element, string name, out ulong value)
    {
        value = 0;
        return element.TryGetProperty(name, out var prop)
               && prop.ValueKind == JsonValueKind.Number
               && prop.TryGetUInt64(out value);
    }

    private static bool TryGetInt64(JsonElement element, string name, out long value)
    {
        value = 0;
        return element.TryGetProperty(name, out var prop)
               && prop.ValueKind == JsonValueKind.Number
               && prop.TryGetInt64(out value);
    }
}
