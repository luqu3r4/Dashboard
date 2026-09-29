namespace DashBoard.Architecture.Tests;

public class ProjectReferenceRulesTests
{
    [Fact]
    public void Repositorio_real_cumple_las_reglas()
        => Assert.Empty(ProjectReferenceRules.FindViolations(
            ProjectReferenceRules.FindRepoRoot(AppContext.BaseDirectory)));

    [Fact]
    public void Detecta_modulo_que_referencia_a_otro_modulo()
        => Assert.Contains(Violations(), v => v.Contains("DashBoard.Modules.A") && v.Contains("DashBoard.Modules.B"));

    [Fact]
    public void Detecta_proyecto_de_modulo_mal_nombrado()
        => Assert.Contains(Violations(), v => v.Contains("MalNombre"));

    [Fact]
    public void Modulo_valido_no_genera_violacion()
        => Assert.DoesNotContain(Violations(), v => v.StartsWith("DashBoard.Modules.B"));

    [Fact]
    public void Detecta_core_con_referencias()
        => Assert.Contains(Violations(), v => v.StartsWith("DashBoard.Core:"));

    private static IReadOnlyList<string> Violations() => ProjectReferenceRules.FindViolations(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "BadRepo"));
}
