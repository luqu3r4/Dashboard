# Infraestructura de módulos + módulo Sistema — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Separar la app en `DashBoard.Web` + `DashBoard.Core` (contrato de módulo), registrar módulos dinámicamente en Inicio/navegación y entregar el módulo Sistema con CPU, RAM, disco, uptime y temperatura en vivo.

**Architecture:** Web referencia a Core y a los módulos; los módulos solo a Core (verificado por un test de arquitectura sobre los `.csproj`). El módulo Sistema lee `/proc` y `/sys` con parsers puros, un `HostMetricsReader` compone un snapshot y un `BackgroundService` lo publica cada 2 s a la página Blazor.

**Tech Stack:** .NET 10 (SDK 10.0.101), Blazor Server (Interactive Server), Bootstrap 5.3.3 local, xUnit, Docker (`dotnet/sdk:10.0` → `dotnet/aspnet:10.0`).

**Spec:** `docs/superpowers/specs/2026-09-29-modulo-sistema-recursos-design.md`

## Global Constraints

- Referencias: `DashBoard.Web` → Core + módulos; `DashBoard.Modules.*` → solo `DashBoard.Core`; `DashBoard.Core` → ningún proyecto.
- Módulos en `src/Modules/DashBoard.Modules.<Nombre>/`; tests en `tests/`.
- Sin dependencias NuGet nuevas en código de producción; tests solo con la plantilla `xunit`.
- Imágenes Docker compatibles con `linux/arm64` y `linux/amd64` (sin cambiar imágenes base).
- UI solo con clases de Bootstrap (sin CSS aislado en el módulo), textos en español.
- Commits Conventional Commits, prefijo en inglés y descripción en español, con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Comandos `docker build/compose up/down/run` los ejecuta el usuario; Claude solo los indica.
- Todos los comandos se ejecutan desde la raíz del repo `C:\Users\ruben\proyectos\DashBoard`.

## Review Focus

1. Cultura `es-ES` de la máquina: `/proc/uptime` usa punto decimal; parsear con `CultureInfo.InvariantCulture` (test en Task 4).
2. Dos lecturas de `/proc/stat` idénticas (Δtotal = 0): % CPU debe ser `null`, nunca `NaN` ni división por cero (test en Task 4).
3. Zona térmica con contenido basura o sin archivo `temp`: se ignora y se usa el resto (test en Task 5).
4. `/proc/meminfo` sin `MemAvailable` (kernels antiguos): RAM `null`, no excepción (test en Task 4).
5. `DiskPath` inexistente (p. ej. al correr en Windows): Disco `null` sin afectar al resto (test en Task 5).

---

### Task 1: Separar DashBoard.Web y DashBoard.Core

**Files:**
- Move: `src/DashBoard.Core/` → `src/DashBoard.Web/` (con `git mv`), `DashBoard.Core.csproj` → `DashBoard.Web.csproj`
- Modify: `src/DashBoard.Web/Program.cs`, `Components/_Imports.razor`, `Components/App.razor` (namespaces `DashBoard.Core*` → `DashBoard.Web*`; hoja `DashBoard.Core.styles.css` → `DashBoard.Web.styles.css`)
- Create: `src/DashBoard.Core/DashBoard.Core.csproj`, `src/DashBoard.Core/IDashboardModule.cs`
- Modify: `DashBoard.sln`, `Dockerfile`, `.dockerignore`

**Interfaces:**
- Produces: `DashBoard.Core.IDashboardModule` exactamente como en el spec §3 (`Title`, `Description`, `Icon`, `Route`, `ConfigureServices(IServiceCollection, IConfiguration)`).

- [ ] **Step 1:** `git mv src/DashBoard.Core src/DashBoard.Web` y `git mv src/DashBoard.Web/DashBoard.Core.csproj src/DashBoard.Web/DashBoard.Web.csproj`. Reemplazar los usos de namespace listados arriba.
- [ ] **Step 2:** Crear Core con `dotnet new classlib -n DashBoard.Core -o src/DashBoard.Core -f net10.0`, borrar `Class1.cs`, añadir `<FrameworkReference Include="Microsoft.AspNetCore.App" />` y el archivo `IDashboardModule.cs`.
- [ ] **Step 3:** Solución: `dotnet sln remove` de la ruta antigua, `dotnet sln add src/DashBoard.Web/DashBoard.Web.csproj src/DashBoard.Core/DashBoard.Core.csproj --solution-folder src`; `dotnet add src/DashBoard.Web reference src/DashBoard.Core`.
- [ ] **Step 4:** `Dockerfile`: copiar `src/DashBoard.Web/DashBoard.Web.csproj` y `src/DashBoard.Core/DashBoard.Core.csproj`, `RUN dotnet restore src/DashBoard.Web/DashBoard.Web.csproj` (no la solución, que incluirá tests), publicar `DashBoard.Web.csproj`, `ENTRYPOINT ["dotnet", "DashBoard.Web.dll"]`. `.dockerignore`: quitar `task/`, añadir `tests/` y `docs/`.
- [ ] **Step 5: Verificar**
  Run: `dotnet build DashBoard.sln` → `Compilación correcta` / 0 errores.
  Run: `dotnet run --project src/DashBoard.Web` en segundo plano y `curl -s -o /dev/null -w "%{http_code}" http://localhost:<puerto de launchSettings>/` → `200`; la página conserva estilos (la respuesta contiene `DashBoard.Web` en el enlace `.styles.css`).
- [ ] **Step 6: Commit** — `refactor: separar DashBoard.Web y DashBoard.Core con contrato de módulo`

### Task 2: Test de arquitectura

**Files:**
- Create: `tests/DashBoard.Architecture.Tests/` (plantilla `xunit`), `ProjectReferenceRules.cs`, `ProjectReferenceRulesTests.cs`
- Create fixtures: `tests/DashBoard.Architecture.Tests/Fixtures/BadRepo/src/DashBoard.Core/DashBoard.Core.csproj` (con una `ProjectReference` a `..\Modules\DashBoard.Modules.B\DashBoard.Modules.B.csproj`), `.../BadRepo/src/Modules/DashBoard.Modules.A/DashBoard.Modules.A.csproj` (referencia a `..\DashBoard.Modules.B\DashBoard.Modules.B.csproj`), `.../BadRepo/src/Modules/DashBoard.Modules.B/DashBoard.Modules.B.csproj` (válido), `.../BadRepo/src/Modules/MalNombre/MalNombre.csproj` (solo referencia Core). Copiar a la salida con `<None Include="Fixtures\**" CopyToOutputDirectory="PreserveNewest" />` y excluirlos de compilación (`<Compile Remove="Fixtures\**" />`).

**Interfaces:**
- Produces: `static class ProjectReferenceRules` con `static string FindRepoRoot(string startDirectory)` (sube hasta encontrar `DashBoard.sln`, lanza `DirectoryNotFoundException` si no) y `static IReadOnlyList<string> FindViolations(string repoRoot)` (un mensaje por violación con formato `"<proyecto infractor>: <problema>"`, p. ej. `"DashBoard.Modules.A: referencia a DashBoard.Modules.B"`). Lee `ProjectReference Include` con `System.Xml.Linq`; compara por nombre de archivo del `.csproj` referenciado.

- [ ] **Step 1: Tests que fallan**

```csharp
[Fact] public void Repositorio_real_cumple_las_reglas()
    => Assert.Empty(ProjectReferenceRules.FindViolations(
        ProjectReferenceRules.FindRepoRoot(AppContext.BaseDirectory)));

[Fact] public void Detecta_modulo_que_referencia_a_otro_modulo()
    => Assert.Contains(Violations(), v => v.Contains("DashBoard.Modules.A") && v.Contains("DashBoard.Modules.B"));

[Fact] public void Detecta_proyecto_de_modulo_mal_nombrado()
    => Assert.Contains(Violations(), v => v.Contains("MalNombre"));

[Fact] public void Modulo_valido_no_genera_violacion()
    => Assert.DoesNotContain(Violations(), v => v.StartsWith("DashBoard.Modules.B"));

[Fact] public void Detecta_core_con_referencias()
    => Assert.Contains(Violations(), v => v.StartsWith("DashBoard.Core:"));

private static IReadOnlyList<string> Violations() => ProjectReferenceRules.FindViolations(
    Path.Combine(AppContext.BaseDirectory, "Fixtures", "BadRepo"));
```

- [ ] **Step 2:** `dotnet sln add tests/DashBoard.Architecture.Tests --solution-folder tests`; `dotnet test tests/DashBoard.Architecture.Tests` → FAIL (no compila: `ProjectReferenceRules` no existe).
- [ ] **Step 3:** Implementar `ProjectReferenceRules`.
- [ ] **Step 4:** `dotnet test tests/DashBoard.Architecture.Tests` → 5 superados.
- [ ] **Step 5: Commit** — `test: añadir test de arquitectura de referencias entre módulos`

### Task 3: Registro de módulos y esqueleto del módulo Sistema

**Files:**
- Create: `src/Modules/DashBoard.Modules.Sistema/DashBoard.Modules.Sistema.csproj` (`dotnet new razorclasslib -f net10.0`, borrar archivos de ejemplo `Component1.razor`, `ExampleJsInterop.cs`, `wwwroot/`), `SistemaModule.cs`, `_Imports.razor`, `Pages/SistemaPage.razor` (temporal: título + texto "En construcción")
- Create: `src/DashBoard.Web/ModuleCatalog.cs`
- Modify: `src/DashBoard.Web/Program.cs`, `Components/Routes.razor`, `Components/Pages/Home.razor`, `Components/Layout/NavMenu.razor`
- Delete: `src/DashBoard.Web/Components/Pages/Sistema.razor`, `Components/Pages/Proyectos.razor`

**Interfaces:**
- Consumes: `IDashboardModule` (Task 1).
- Produces: `sealed class ModuleCatalog(IReadOnlyList<IDashboardModule> modules)` con `IReadOnlyList<IDashboardModule> Modules` y `IReadOnlyList<Assembly> Assemblies` (ensamblados distintos de los módulos). `SistemaModule : IDashboardModule` con `Title = "Sistema / Homelab"`, `Description = "CPU, memoria, disco, temperatura y tiempo encendida de la máquina."`, `Icon = "bi-hdd-network-fill"`, `Route = "sistema"`; `ConfigureServices` vacío en esta task (Task 6 lo rellena). Página con `@page "/sistema"`.

- [ ] **Step 1:** Crear el proyecto del módulo, `dotnet sln add ... --solution-folder Modules` (carpeta anidada bajo `src` si el CLI lo permite; si no, `src`), `dotnet add src/Modules/DashBoard.Modules.Sistema reference src/DashBoard.Core`, `dotnet add src/DashBoard.Web reference src/Modules/DashBoard.Modules.Sistema`.
- [ ] **Step 2:** `Program.cs`: `IDashboardModule[] modules = [new SistemaModule()];` → `ConfigureServices` de cada uno, `AddSingleton<IDashboardModule>(m)` de cada uno, `AddSingleton(new ModuleCatalog(modules))`; `MapRazorComponents<App>().AddInteractiveServerRenderMode().AddAdditionalAssemblies([.. catalog.Assemblies])`. `Routes.razor`: `@inject ModuleCatalog Catalog` y `AdditionalAssemblies="Catalog.Assemblies"`.
- [ ] **Step 3:** `Home.razor` y `NavMenu.razor`: inyectar `IEnumerable<IDashboardModule>` y generar tarjetas/enlaces con el marcado actual (Inicio sigue fijo en la nav). Borrar los dos placeholders.
- [ ] **Step 4:** `Dockerfile`: añadir `COPY src/Modules/DashBoard.Modules.Sistema/DashBoard.Modules.Sistema.csproj src/Modules/DashBoard.Modules.Sistema/` antes del restore.
- [ ] **Step 5: Verificar**
  Run: `dotnet test` → todos los tests pasan (incluido `Repositorio_real_cumple_las_reglas` con el módulo nuevo).
  Run: `dotnet run --project src/DashBoard.Web`; `curl -s http://localhost:<puerto>/` contiene `Sistema / Homelab` y `href="sistema"`, no contiene `Proyectos`; `curl -s -o /dev/null -w "%{http_code}" http://localhost:<puerto>/sistema` → `200`; `/proyectos` → `404`.
- [ ] **Step 6: Commit** — `feat: registrar módulos dinámicamente y añadir esqueleto del módulo Sistema`

### Task 4: Parsers de /proc y /sys

**Files:**
- Create: `src/Modules/DashBoard.Modules.Sistema/Metrics/UsageBytes.cs`, `CpuTimes.cs`, `CpuStatParser.cs`, `MemInfoParser.cs`, `UptimeParser.cs`, `ThermalParser.cs`
- Create: `tests/DashBoard.Modules.Sistema.Tests/` (plantilla `xunit`, referencia al módulo), `ParserTests.cs`, fixtures `Fixtures/stat.txt`, `Fixtures/meminfo.txt` (copias literales de un Linux real, copiadas a la salida)

**Interfaces:**
- Produces (namespace `DashBoard.Modules.Sistema.Metrics`, todas `public static` salvo los records):
  - `sealed record UsageBytes(long Used, long Total)` con `double Percent => Total == 0 ? 0 : Used * 100.0 / Total`.
  - `readonly record struct CpuTimes(ulong Total, ulong Idle)` con `double? UsagePercentSince(CpuTimes previous)` → `null` si `Total <= previous.Total`; si no, `(1 − ΔIdle/ΔTotal) × 100`.
  - `CpuStatParser.TryParse(string content, out CpuTimes times) : bool` — línea que empieza por `"cpu "`; Total = suma de todos los campos numéricos; Idle = campo 4 (idle) + campo 5 (iowait).
  - `MemInfoParser.TryParse(string content, out UsageBytes? usage) : bool` — `MemTotal` y `MemAvailable` en kB × 1024; Used = Total − Available.
  - `UptimeParser.TryParse(string content, out TimeSpan uptime) : bool` — primer número, `CultureInfo.InvariantCulture`.
  - `ThermalParser.TryParse(string content, out double celsius) : bool` — entero en miligrados / 1000.

- [ ] **Step 1: Tests que fallan** (`[Fact]` salvo indicación):
  - `CpuStat_fixture_real`: `TryParse(stat.txt)` → `true`, `Total` = suma de la línea `cpu` del fixture, `Idle` = idle + iowait de esa línea.
  - `CpuStat_texto_vacio_o_sin_linea_cpu` (`[Theory]` con `""` y `"intr 1 2 3"`) → `false`.
  - `CpuUsage_entre_dos_lecturas`: `new CpuTimes(200, 150).UsagePercentSince(new CpuTimes(100, 100))` → `50.0`.
  - `CpuUsage_sin_cambios_es_null`: `new CpuTimes(100, 50).UsagePercentSince(new CpuTimes(100, 50))` → `null`.
  - `MemInfo_fixture_real`: `Total` = MemTotal × 1024 y `Used` = (MemTotal − MemAvailable) × 1024 del fixture.
  - `MemInfo_sin_MemAvailable`: `"MemTotal: 1000 kB\nMemFree: 500 kB"` → `false`.
  - `Uptime_con_cultura_espanola`: con `CultureInfo.CurrentCulture = new("es-ES")`, `TryParse("350735.47 234388.90")` → `true`, `TimeSpan.FromSeconds(350735.47)`.
  - `Uptime_basura` → `false` para `"abc"`.
  - `Thermal_miligrados`: `"45250\n"` → `45.25`.
  - `Thermal_basura` → `false` para `""` y `"n/a"`.
- [ ] **Step 2:** `dotnet sln add tests/DashBoard.Modules.Sistema.Tests --solution-folder tests`; `dotnet test tests/DashBoard.Modules.Sistema.Tests` → FAIL (no compila).
- [ ] **Step 3:** Implementar records y parsers con las firmas de arriba.
- [ ] **Step 4:** `dotnet test` → todo en verde.
- [ ] **Step 5: Commit** — `feat: añadir parsers de métricas de /proc y /sys`

### Task 5: HostMetricsReader y configuración

**Files:**
- Create: `src/Modules/DashBoard.Modules.Sistema/SistemaOptions.cs`, `Metrics/HostMetrics.cs`, `Metrics/HostMetricsReader.cs`
- Test: `tests/DashBoard.Modules.Sistema.Tests/HostMetricsReaderTests.cs`

**Interfaces:**
- Consumes: parsers y records de Task 4.
- Produces:
  - `sealed class SistemaOptions { string ProcPath = "/proc"; string SysPath = "/sys"; string DiskPath = "/"; TimeSpan RefreshInterval = TimeSpan.FromSeconds(2); }` (propiedades `get; set;`), sección de configuración `"Sistema"` (const `SistemaOptions.SectionName`).
  - `sealed record HostMetrics(double? CpuPercent, UsageBytes? Memory, UsageBytes? Disk, TimeSpan? Uptime, double? TemperatureCelsius, DateTimeOffset Timestamp)`.
  - `sealed class HostMetricsReader(IOptions<SistemaOptions> options, ILogger<HostMetricsReader> logger, TimeProvider timeProvider)` con `HostMetrics Read()`. Guarda la última `CpuTimes` para calcular el % en la siguiente llamada (primera llamada → `CpuPercent = null`). Temperatura = máximo de `SysPath/class/thermal/thermal_zone*/temp` válidos. Disco con `new DriveInfo(DiskPath)` (`Used = TotalSize − AvailableFreeSpace`). Cada métrica en su propio `try/catch`; al fallar registra `LogWarning` solo si esa métrica no estaba ya marcada como fallida (conjunto interno por nombre); al recuperarse se desmarca.

- [ ] **Step 1: Tests que fallan.** Helper que crea un directorio temporal con `proc/stat`, `proc/meminfo`, `proc/uptime`, `sys/class/thermal/thermal_zone0/temp` = `41000`, `thermal_zone1/temp` = `52500`, y opciones apuntando a él con `DiskPath` = el propio directorio temporal. Logger: implementación mínima en el test que cuenta llamadas de nivel `Warning`.
  - `Lee_todas_las_metricas`: `Memory`, `Uptime`, `Disk` no nulos; `TemperatureCelsius == 52.5`; `CpuPercent == null` en la primera lectura.
  - `Segunda_lectura_calcula_cpu`: reescribir `stat` con contadores mayores entre lecturas → `CpuPercent` con el valor esperado.
  - `Zona_termica_rota_se_ignora`: `thermal_zone1/temp` = `"basura"` y añadir `thermal_zone2` sin archivo `temp` → `TemperatureCelsius == 41.0`.
  - `Fuente_rota_solo_anula_su_metrica`: borrar `proc/meminfo` → `Memory == null`, `Uptime` y `TemperatureCelsius` no nulos.
  - `Disco_inexistente_es_null`: `DiskPath` = ruta que no existe → `Disk == null`, el resto no nulo.
  - `Fallo_repetido_se_registra_una_vez`: sin `meminfo`, tres `Read()` → 1 warning; crear `meminfo`, `Read()`, borrarlo, `Read()` → 2 warnings en total.
- [ ] **Step 2:** `dotnet test tests/DashBoard.Modules.Sistema.Tests` → FAIL (no compila).
- [ ] **Step 3:** Implementar `SistemaOptions`, `HostMetrics`, `HostMetricsReader`.
- [ ] **Step 4:** `dotnet test` → todo en verde.
- [ ] **Step 5: Commit** — `feat: añadir lector de métricas de la máquina con fallos aislados`

### Task 6: Muestreo en segundo plano y página /sistema

**Files:**
- Create: `src/Modules/DashBoard.Modules.Sistema/Metrics/HostMetricsSampler.cs`, `Pages/MetricsFormat.cs`
- Modify: `SistemaModule.cs` (`ConfigureServices`), `Pages/SistemaPage.razor`
- Test: `tests/DashBoard.Modules.Sistema.Tests/MetricsFormatTests.cs`

**Interfaces:**
- Consumes: `HostMetricsReader`, `HostMetrics`, `SistemaOptions` (Task 5).
- Produces:
  - `sealed class HostMetricsSampler(HostMetricsReader reader, IOptions<SistemaOptions> options) : BackgroundService` con `HostMetrics? Current { get; }` y `event Action? Updated`. Bucle con `PeriodicTimer(RefreshInterval)`: lee, actualiza `Current`, dispara `Updated`. Lee una vez al arrancar, antes del primer tick.
  - `static class MetricsFormat`: `string Uptime(TimeSpan)`, `string Gigabytes(long bytes)`, `string TemperatureClass(double celsius)`.
  - `SistemaModule.ConfigureServices`: `Configure<SistemaOptions>(configuration.GetSection(SistemaOptions.SectionName))`, `TryAddSingleton(TimeProvider.System)`, `AddSingleton<HostMetricsReader>()`, `AddSingleton<HostMetricsSampler>()`, `AddHostedService(sp => sp.GetRequiredService<HostMetricsSampler>())`.

- [ ] **Step 1: Tests que fallan**
  - `Uptime_formatos` (`[Theory]`): `3d 4h 12m` → `"3 d 4 h 12 min"`; `2h 0m` → `"2 h 0 min"`; `5m` → `"5 min"`; `0` → `"0 min"`. (Días solo si ≥ 1; horas si hay días u horas ≥ 1; minutos siempre.)
  - `Gigabytes_una_decimal`: `1610612736` → `"1,5 GB"` bajo `es-ES` (usa la cultura actual).
  - `TemperatureClass_umbrales` (`[Theory]`): `69.9` → `""`, `70` → `"text-warning"`, `84.9` → `"text-warning"`, `85` → `"text-danger"`.
- [ ] **Step 2:** `dotnet test tests/DashBoard.Modules.Sistema.Tests` → FAIL.
- [ ] **Step 3:** Implementar `MetricsFormat`, `HostMetricsSampler` y `ConfigureServices`.
- [ ] **Step 4:** `SistemaPage.razor`: `@rendermode InteractiveServer`, `@implements IDisposable`, `@inject HostMetricsSampler Sampler`. `OnInitialized` suscribe a `Updated` con un handler que hace `InvokeAsync(StateHasChanged)`; `Dispose` desuscribe. `PageTitle` y `h1` = `Sistema / Homelab`. Cinco tarjetas Bootstrap en `row row-cols-1 row-cols-sm-2 row-cols-lg-3 g-3`: CPU (% + `progress`), RAM (`usado / total` con `MetricsFormat.Gigabytes` + `progress`), Disco (ídem), Temperatura (`45,3 °C` con `MetricsFormat.TemperatureClass`), Tiempo encendida (`MetricsFormat.Uptime`). Valor `null` (o `Sampler.Current == null`) → `No disponible` en `text-secondary`.
- [ ] **Step 5: Verificar**
  Run: `dotnet test` → todo en verde.
  Run: `dotnet run --project src/DashBoard.Web` (Windows); `curl -s http://localhost:<puerto>/sistema` → `200`, contiene 5 veces `No disponible`; la consola no muestra excepciones no controladas (solo los warnings de métrica una vez cada uno).
- [ ] **Step 6: Commit** — `feat: mostrar métricas de la máquina en vivo en el módulo Sistema`

### Task 7: Docker, CLAUDE.md y verificación final

**Files:**
- Modify: `docker-compose.yml` (montajes y variables exactamente como spec §6), `CLAUDE.md`

- [ ] **Step 1:** `docker-compose.yml`: añadir bajo `dashboard` los tres montajes `:ro` y las tres variables `Sistema__*` del spec §6.
- [ ] **Step 2:** `CLAUDE.md`: sección de arquitectura con el papel de `DashBoard.Web` (aplicación, registra módulos) y `DashBoard.Core` (contrato `IDashboardModule`); pasos para añadir un módulo (crear `src/Modules/DashBoard.Modules.<Nombre>` como Razor class library → referencia a Core → referencia desde Web → línea en la lista de `Program.cs` → `COPY` del `.csproj` en el `Dockerfile` → proyecto `tests/DashBoard.Modules.<Nombre>.Tests`); mención de que el test de arquitectura hace cumplir las reglas; comandos `dotnet test DashBoard.sln` y `dotnet run --project src/DashBoard.Web`.
- [ ] **Step 3: Verificar localmente**
  Run: `dotnet build DashBoard.sln` y `dotnet test DashBoard.sln` → 0 errores, todos los tests superados.
- [ ] **Step 4: Verificación Docker (la ejecuta el usuario)**
  Pedir al usuario: `docker compose up --build`, abrir `http://localhost:8080/sistema` y confirmar que las cinco tarjetas muestran valores (temperatura puede ser `No disponible` en la VM de WSL2) y que CPU/uptime cambian cada 2 s; después `docker compose down`.
- [ ] **Step 5: Commit** — `feat: montar métricas de la máquina en Docker y documentar módulos`
