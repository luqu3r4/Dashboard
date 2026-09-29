using System.Xml.Linq;

namespace DashBoard.Architecture.Tests;

/// <summary>
/// Reglas de referencias entre proyectos: Core no referencia nada y los módulos
/// (src/Modules) solo pueden referenciar a DashBoard.Core.
/// </summary>
public static class ProjectReferenceRules
{
    private const string CoreName = "DashBoard.Core";
    private const string ModulePrefix = "DashBoard.Modules.";

    public static string FindRepoRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DashBoard.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"No se encontró DashBoard.sln subiendo desde '{startDirectory}'.");
    }

    public static IReadOnlyList<string> FindViolations(string repoRoot)
    {
        var violations = new List<string>();

        var corePath = Path.Combine(repoRoot, "src", CoreName, CoreName + ".csproj");
        if (File.Exists(corePath))
        {
            foreach (var reference in ReadReferences(corePath))
                violations.Add($"{CoreName}: referencia a {reference}");
        }

        var modulesDir = Path.Combine(repoRoot, "src", "Modules");
        if (!Directory.Exists(modulesDir))
            return violations;

        foreach (var csproj in Directory.EnumerateFiles(modulesDir, "*.csproj", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(csproj);

            if (!name.StartsWith(ModulePrefix, StringComparison.Ordinal))
                violations.Add($"{name}: el nombre debe empezar por {ModulePrefix}");

            foreach (var reference in ReadReferences(csproj).Where(r => r != CoreName))
                violations.Add($"{name}: referencia a {reference}");
        }

        return violations;
    }

    private static IEnumerable<string> ReadReferences(string csprojPath)
        => XDocument.Load(csprojPath)
            .Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/').Split('/')[^1]));
}
