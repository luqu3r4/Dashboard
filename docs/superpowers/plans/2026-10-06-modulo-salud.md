# Módulo Salud Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mostrar en `/salud` del DashBoard pasos, distancia, peso y entrenamientos que la app Android sincroniza en SaludApi, con resumen de hoy/semana, evolución por periodo y objetivos.

**Architecture:** SaludApi gana un endpoint de resumen diario (`GET /api/health-records/daily`) que agrega en C# (`DailyAggregator`) por día en hora local. DashBoard gana el módulo `DashBoard.Modules.Salud`, que consulta SaludApi por HTTP con la clave de consulta a través de una red Docker externa compartida `homelab`, calcula resúmenes con funciones puras y pinta gráficas SVG en Razor.

**Tech Stack:** .NET 10, ASP.NET Core (Web API / Blazor Server), EF Core (Npgsql; InMemory en tests), xUnit, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-10-06-modulo-salud-design.md`

## Global Constraints

- Ramas: SaludApi `feature/resumen-diario` (desde `master`), DashBoard `feature/modulo-salud` (ya creada, contiene el spec).
- **Commits:** para este plan el usuario autorizó (2026-10-07) que se hagan sin pedir confirmación, con los mensajes de cada tarea. Conventional Commits con prefijo en inglés y descripción en español; terminan con las líneas `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` y `Claude-Session: https://claude.ai/code/session_01YZ1mtyQC5MeXgY3Wq9eQHL`. Nunca `--no-verify`, `--amend` ni push.
- Sin dependencias NuGet nuevas en ninguno de los dos proyectos.
- Reglas de módulos de `DashBoard/CLAUDE.md`: el módulo solo referencia `DashBoard.Core`; ningún módulo referencia a otro.
- Imágenes compatibles con `linux/arm64` y `linux/amd64`; no se añaden imágenes nuevas.
- Zona horaria por defecto `Europe/Madrid`; semana de lunes a domingo; formato numérico `es-ES`.
- Tipos admitidos por el endpoint diario: exactamente `Steps`, `Distance`, `Weight`, `ExerciseSession`. Rango máximo 366 días.
- Textos de estado de la página, literales: "El módulo Salud no está configurado (falta `Salud__ApiUrl`).", "No se puede conectar con SaludApi.", "SaludApi ha rechazado la clave (revisa `Salud__ApiKey`).", "Sin datos".
- Los comandos de Docker los ejecuta Claude (autorizado por el usuario), explicándolos.

## Review Focus

- **Medianoche en Madrid vs. UTC del contenedor:** a las 00:30 en Madrid, "hoy" es el día de Madrid aunque el reloj UTC siga en el día anterior → test de `SaludClock.Today` (Task 4).
- **Sin datos todavía** (móvil sin sincronizar o periodo vacío): series vacías no deben dividir por cero ni romper la página → tests de `ChartGeometry` con lista vacía (Task 6) y de `SaludSummary` con listas vacías (Task 5).
- **Zona horaria mal escrita en la configuración** (`Health__TimeZone=Europa/Madrid`): debe fallar al arrancar con un mensaje que nombre el valor, no con un 500 en cada petición → test de `TimeZoneResolver.Resolve` (Task 2).
- **SaludApi colgada o lenta:** la página no debe quedarse cargando indefinidamente → test de timeout en `SaludApiClient` que devuelve `Unreachable` (Task 4).
- **Una sola medición de peso / sin medición de hace 30 días:** la variación debe quedar vacía y la gráfica de una sola línea no debe dividir por cero → tests en `SaludSummary.Weight` (Task 5) y `ChartGeometry.LineScale` con un punto (Task 6).

---

## Parte A — SaludApi (`C:\Users\ruben\proyectos\homelab\SaludApi`)

### Task 1: `DailyAggregator`

**Files:**
- Create: `src/SaludApi.Api/Contracts/DailyValueDto.cs`
- Create: `src/SaludApi.Api/Summaries/DailyAggregator.cs`
- Test: `tests/SaludApi.Api.Tests/DailyAggregatorTests.cs`

**Interfaces:**
- Consumes: `HealthRecord` (`Data/HealthRecord.cs`: `RecordType`, `StartTime`, `EndTime`, `PayloadJson`).
- Produces:
  - `public record DailyValueDto(DateOnly Date, double Value, int Count);` (namespace `SaludApi.Api.Contracts`; `DateOnly` se serializa como `"yyyy-MM-dd"`).
  - `public static class DailyAggregator` (namespace `SaludApi.Api.Summaries`):
    - `public static IReadOnlyList<string> SupportedTypes { get; }` = `Steps`, `Distance`, `Weight`, `ExerciseSession`.
    - `public static List<DailyValueDto> Aggregate(IEnumerable<HealthRecord> records, string type, TimeZoneInfo timeZone)` — ordenado por fecha; lanza `ArgumentException` si `type` no está en `SupportedTypes`.

- [ ] **Step 1: Crear la rama**

Run: `git checkout master && git checkout -b feature/resumen-diario`
Expected: `Switched to a new branch 'feature/resumen-diario'`

- [ ] **Step 2: Escribir los tests que fallan** (`TimeZoneInfo madrid = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid")`; registros construidos con un helper `Rec(type, startUtc, endUtc?, payload)`)

- `SumsStepsPerLocalDay`: `{"count":100}` a 2026-09-10T08:00Z y `{"count":50}` a 2026-09-10T18:00Z → `[(2026-09-10, 150, 2)]`.
- `SumsDistanceMeters`: `{"meters":1000.5}` + `{"meters":499.5}` mismo día → `Value == 1500`, `Count == 2`.
- `RecordAfterLocalMidnightCountsForThatDay`: `{"count":10}` a 2026-09-09T22:30Z (00:30 del 10 en Madrid, verano) → fecha `2026-09-10`.
- `UsesWinterOffsetAfterDstChange`: `{"count":10}` a 2026-10-25T23:30Z (00:30 del 26 en Madrid, invierno, UTC+1) → fecha `2026-10-26`.
- `WeightTakesLastMeasurementOfTheDay`: `{"kg":86.4}` a 07:00Z y `{"kg":86.0}` a 20:00Z del mismo día → `Value == 86.0`, `Count == 2`.
- `ExerciseSessionSumsMinutes`: sesiones de 60 y 30 minutos el mismo día → `Value == 90`, `Count == 2`.
- `IgnoresRecordsWithoutExpectedField`: `{"count":100}` + `{}` + sesión sin `EndTime` (en un test de `ExerciseSession`) → el registro inválido no suma ni cuenta.
- `DaysWithoutRecordsAreOmitted`: registros los días 1 y 3 → dos elementos.
- `RejectsUnsupportedType`: `Aggregate([], "HeartRate", madrid)` → `Assert.Throws<ArgumentException>`.

- [ ] **Step 3: Ejecutar y ver que fallan**

Run: `dotnet test --filter DailyAggregatorTests`
Expected: error de compilación (`DailyAggregator` no existe).

- [ ] **Step 4: Implementar `DailyValueDto` y `DailyAggregator`**

Día local = `DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(record.StartTime, timeZone).DateTime)`. Leer el payload con `JsonDocument` y `TryGetProperty`; payload no parseable o sin el campo → registro ignorado.

- [ ] **Step 5: Ejecutar y ver que pasan**

Run: `dotnet test --filter DailyAggregatorTests`
Expected: 9 tests PASS.

- [ ] **Step 6: Commit** 

```bash
git add src/SaludApi.Api/Contracts/DailyValueDto.cs src/SaludApi.Api/Summaries tests/SaludApi.Api.Tests/DailyAggregatorTests.cs
git commit -m "feat: añadir agregador diario de registros de salud"
```

### Task 2: Endpoint `GET /api/health-records/daily` y red `homelab`

**Files:**
- Create: `src/SaludApi.Api/HealthOptions.cs`
- Create: `src/SaludApi.Api/Summaries/TimeZoneResolver.cs`
- Modify: `src/SaludApi.Api/Controllers/HealthRecordsController.cs` (constructor + acción nueva)
- Modify: `src/SaludApi.Api/Program.cs` (registrar opciones y `TimeZoneInfo`)
- Modify: `tests/SaludApi.Api.Tests/HealthRecordsControllerTests.cs`, `tests/SaludApi.Api.Tests/HealthRecordsQueryTests.cs` (nuevo constructor del controlador)
- Modify: `docker-compose.yml`, `README.md`, `CLAUDE.md`
- Test: `tests/SaludApi.Api.Tests/HealthRecordsDailyTests.cs`, `tests/SaludApi.Api.Tests/TimeZoneResolverTests.cs`

**Interfaces:**
- Consumes: `DailyAggregator.Aggregate`, `DailyAggregator.SupportedTypes`, `DailyValueDto` (Task 1).
- Produces:
  - `public sealed class HealthOptions { public const string SectionName = "Health"; public string TimeZone { get; set; } = "Europe/Madrid"; }`
  - `public static class TimeZoneResolver { public static TimeZoneInfo Resolve(string id); }` — lanza `InvalidOperationException` cuyo mensaje contiene `id` si no existe.
  - Constructor: `HealthRecordsController(SaludApiDbContext db, TimeZoneInfo timeZone)`.
  - Acción: `[HttpGet("daily")] [Authorize(Policy = ApiKeyScopes.QueryPolicy)] Task<ActionResult<List<DailyValueDto>>> Daily([FromQuery] string? type, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)`.

- [ ] **Step 1: Escribir los tests que fallan**

`TimeZoneResolverTests`:
- `ResolvesEuropeMadrid`: `Resolve("Europe/Madrid").Id == "Europe/Madrid"`.
- `ThrowsWithTheInvalidIdInTheMessage`: `Resolve("Europa/Madrid")` → `InvalidOperationException` con `Message` que contiene `"Europa/Madrid"`.

`HealthRecordsDailyTests` (patrón de `HealthRecordsQueryTests`: InMemory + controlador con `TimeZoneResolver.Resolve("Europe/Madrid")`):
- `ReturnsDailyValuesForTheRange`: pasos el 2026-09-10 y 2026-09-11, `from=2026-09-10&to=2026-09-11` → `OkObjectResult` con 2 `DailyValueDto`.
- `ExcludesRecordsOutsideTheLocalRange`: con `from=to=2026-09-10`, tres registros de pasos — 2026-09-09T21:59Z (23:59 del 9 en Madrid, fuera), 2026-09-09T22:00Z (00:00 del 10, dentro) y 2026-09-10T22:00Z (00:00 del 11, fuera) → un único `DailyValueDto(2026-09-10, …, Count 1)`.
- `RejectsUnsupportedType`: `type=HeartRate` → `BadRequestObjectResult` cuyo texto contiene `Steps`.
- `RejectsMissingDates`: `from` nulo → `BadRequestObjectResult`.
- `RejectsFromAfterTo` → `BadRequestObjectResult`.
- `RejectsRangeOver366Days`: `from=2025-01-01&to=2026-01-02` → `BadRequestObjectResult`.
- `RequiresQueryPolicy`: por reflexión, `typeof(HealthRecordsController).GetMethod("Daily")` tiene `AuthorizeAttribute` con `Policy == ApiKeyScopes.QueryPolicy`.

- [ ] **Step 2: Ejecutar y ver que fallan**

Run: `dotnet test`
Expected: errores de compilación (`TimeZoneResolver`, `Daily` no existen).

- [ ] **Step 3: Implementar `HealthOptions`, `TimeZoneResolver` y la acción `Daily`**

Rango UTC: `[from 00:00 local, (to+1) 00:00 local)` con `TimeZoneInfo.ConvertTimeToUtc`, construido como `DateTimeOffset` con offset cero (Npgsql exige offset 0 para `timestamptz`). Filtrar por `RecordType == type`, `StartTime >= inicio`, `StartTime < fin`; luego `DailyAggregator.Aggregate`. Mensaje de tipo no admitido: `"'type' debe ser uno de: Steps, Distance, Weight, ExerciseSession."`.
En `Program.cs`: `Configure<HealthOptions>` con la sección `Health` y `AddSingleton(sp => TimeZoneResolver.Resolve(...TimeZone))`; resolverlo una vez tras `Build()` para que una zona inválida falle al arrancar. Actualizar las construcciones del controlador en los tests existentes.

- [ ] **Step 4: Ejecutar todos los tests**

Run: `dotnet test`
Expected: todos PASS (los existentes y los nuevos).

- [ ] **Step 5: Red `homelab` y documentación**

`docker-compose.yml`: el servicio `saludapi` lleva `networks: [default, homelab]`; `db` no cambia; al final:

```yaml
networks:
  homelab:
    external: true
```

`README.md` y `CLAUDE.md`: endpoint nuevo (tabla de agregación del spec §2.2), `Health__TimeZone`, y el paso previo `docker network create homelab`. Comentar en el compose qué es una red externa (primera vez que aparece).

- [ ] **Step 6: Verificar en Docker**

Run: `docker network create homelab` (si no existe) y `docker compose up --build -d`; después
`curl -s -H "X-Api-Key: <query key de .env>" "http://localhost:8081/api/health-records/daily?type=Steps&from=2026-09-06&to=2026-10-06"`
Expected: JSON con un elemento por día y `value` > 0; con la clave de ingesta → 403.

- [ ] **Step 7: Commit** 

```bash
git add src tests docker-compose.yml README.md CLAUDE.md
git commit -m "feat: añadir endpoint de resumen diario y red Docker homelab"
```

---

## Parte B — DashBoard (`C:\Users\ruben\proyectos\homelab\DashBoard`)

### Task 3: Esqueleto del módulo Salud

**Files:**
- Create: `src/Modules/DashBoard.Modules.Salud/DashBoard.Modules.Salud.csproj` (copia de la de Sistema)
- Create: `src/Modules/DashBoard.Modules.Salud/SaludModule.cs`, `SaludOptions.cs`, `_Imports.razor`, `Pages/SaludPage.razor` (provisional: título "Salud")
- Create: `tests/DashBoard.Modules.Salud.Tests/DashBoard.Modules.Salud.Tests.csproj` (como la de Sistema, sin fixtures), `tests/DashBoard.Modules.Salud.Tests/FakeSaludHandler.cs` (mismo patrón que `FakeDockerHandler`)
- Modify: `DashBoard.sln`, `src/DashBoard.Web/DashBoard.Web.csproj`, `src/DashBoard.Web/Program.cs:16` (`[new SistemaModule(), new SaludModule()]`), `Dockerfile` (COPY del `.csproj` antes del restore)
- Test: `tests/DashBoard.Modules.Salud.Tests/SaludModuleTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class SaludOptions` (namespace `DashBoard.Modules.Salud`): `SectionName = "Salud"`; `string? ApiUrl`; `string? ApiKey`; `string TimeZone = "Europe/Madrid"`; `int? StepsPerDay`; `int? WorkoutsPerWeek`; `double? TargetWeightKg`.
  - `SaludModule : IDashboardModule` con `Title "Salud"`, `Route "salud"`, `Icon "bi-heart-pulse-fill"`, `Description "Pasos, peso y entrenamientos sincronizados desde el móvil."`.
  - `FakeSaludHandler` con `Requests` (lista de `PathAndQuery`) y `Headers` (cabecera `X-Api-Key` de cada petición).

- [ ] **Step 1: Test que falla:** `SaludModuleTests.ExposesTitleRouteAndIcon` comprueba los cuatro valores anteriores; `BindsOptionsFromSaludSection` construye `ConfigurationBuilder().AddInMemoryCollection` con `Salud:ApiUrl`, `Salud:StepsPerDay=10000`, llama a `ConfigureServices` y resuelve `IOptions<SaludOptions>` con esos valores.
- [ ] **Step 2:** `dotnet test DashBoard.sln` → falla (no compila).
- [ ] **Step 3:** Crear proyectos, añadirlos a la solución (`dotnet sln add`), referencias, `Program.cs`, `Dockerfile`.
- [ ] **Step 4:** `dotnet test DashBoard.sln` → PASS, incluidos `DashBoard.Architecture.Tests`.
- [ ] **Step 5: Commit**: `feat: añadir esqueleto del módulo Salud`

### Task 4: `SaludApiClient` y `SaludClock`

**Files:**
- Create: `src/Modules/DashBoard.Modules.Salud/Api/SaludApiClient.cs`, `Api/SaludResult.cs`, `Api/DailyValue.cs`, `Api/Workout.cs`, `SaludClock.cs`
- Modify: `SaludModule.cs` (registrar `TimeProvider`, `TimeZoneInfo` y el cliente)
- Test: `tests/DashBoard.Modules.Salud.Tests/SaludApiClientTests.cs`, `SaludClockTests.cs`

**Interfaces:**
- Consumes: `SaludOptions` (Task 3).
- Produces (namespace `DashBoard.Modules.Salud.Api` salvo `SaludClock`):
  - `public enum SaludStatus { Ok, NotConfigured, Unreachable, Unauthorized, InvalidResponse }`
  - `public sealed record SaludResult<T>(SaludStatus Status, T? Data)` con `static SaludResult<T> Ok(T data)` y `static SaludResult<T> Fail(SaludStatus status)`.
  - `public sealed record DailyValue(DateOnly Date, double Value, int Count);`
  - `public sealed record Workout(DateTimeOffset Start, string Title, double Minutes);`
  - `public sealed class SaludApiClient(HttpClient http, SaludOptions options)`:
    - `Task<SaludResult<IReadOnlyList<DailyValue>>> GetDailyAsync(string type, DateOnly from, DateOnly to, CancellationToken ct)`
    - `Task<SaludResult<IReadOnlyList<Workout>>> GetWorkoutsAsync(DateTimeOffset fromUtc, CancellationToken ct)`
  - `public static class SaludClock { public static DateOnly Today(TimeProvider time, TimeZoneInfo zone); }`

- [ ] **Step 1: Tests que fallan** (`SaludApiClient` sobre `HttpClient(new FakeSaludHandler(...)) { BaseAddress = new("http://saludapi:8080") }`):
  - `GetDailySendsTypeDatesAndApiKey`: pide `/api/health-records/daily?type=Steps&from=2026-09-01&to=2026-09-30` con cabecera `X-Api-Key` = `options.ApiKey`.
  - `GetDailyParsesValues`: respuesta `[{"date":"2026-09-06","value":8432,"count":37}]` → `Ok` con `DailyValue(2026-09-06, 8432, 37)`.
  - `NotConfiguredWhenApiUrlIsBlank`: `ApiUrl = ""` → `NotConfigured` y `Requests` vacío.
  - `UnauthorizedOn401And403`: dos casos → `Unauthorized`.
  - `UnreachableOnNetworkError`: el handler lanza `HttpRequestException` → `Unreachable`.
  - `UnreachableOnTimeout`: el handler espera más que `HttpClient.Timeout` (fijar 100 ms en el test) → `Unreachable`.
  - `InvalidResponseOnMalformedJson`: cuerpo `"no json"` → `InvalidResponse`.
  - `GetWorkoutsMapsTitleAndMinutes`: registro con `startTime 10:00Z`, `endTime 11:02Z`, `payloadJson "{\"exerciseType\":70,\"title\":\"Pierna\"}"` → `Workout(10:00Z, "Pierna", 62)`; sin `title` → `"Entrenamiento"`; sin `endTime` → se descarta. Pide `/api/health-records?type=ExerciseSession&from=<fromUtc ISO 8601 escapado>`.
  - `SaludClockTests.UsesLocalDateAfterMidnight`: `FakeTimeProvider` fijado a 2026-09-09T22:30Z con Madrid → `2026-09-10` (usar un `TimeProvider` propio de test que sobrescriba `GetUtcNow`, sin paquetes nuevos).
- [ ] **Step 2:** `dotnet test DashBoard.sln` → falla.
- [ ] **Step 3:** Implementar. Timeout del `HttpClient` registrado en el módulo: 5 s; mismo estilo de registro que `DockerApiClient` en `SistemaModule` (singleton con `SocketsHttpHandler`). `TaskCanceledException` sin cancelación del llamador = timeout → `Unreachable`. Respuesta JSON con `JsonSerializerDefaults.Web`.
- [ ] **Step 4:** `dotnet test DashBoard.sln` → PASS.
- [ ] **Step 5: Commit**: `feat: añadir cliente de SaludApi al módulo Salud`

### Task 5: `SaludSummary`

**Files:**
- Create: `src/Modules/DashBoard.Modules.Salud/Summary/SaludSummary.cs`, `Summary/Period.cs`, `Summary/ChartPoint.cs`
- Test: `tests/DashBoard.Modules.Salud.Tests/SaludSummaryTests.cs`

**Interfaces:**
- Consumes: `DailyValue`, `Workout` (Task 4).
- Produces (namespace `DashBoard.Modules.Salud.Summary`):
  - `public enum Period { Days30, Days90, Year }`
  - `public sealed record ChartPoint(string Label, double Value, string Tooltip);`
  - `public sealed record TodayStats(double Steps, double Km, double? StepsGoalPercent);`
  - `public sealed record WeekWorkouts(int Count, int? Goal);`
  - `public sealed record WeightStats(double CurrentKg, DateOnly Date, double? ChangeOver30Days, double? DistanceToTarget);`
  - `public static class SaludSummary`:
    - `(DateOnly From, DateOnly To) Range(Period period, DateOnly today)` — 30 días: `today-29..today`; 90: `today-89..today`; año: `today-364..today`.
    - `DateOnly MondayOf(DateOnly day)`
    - `TodayStats Today(IReadOnlyList<DailyValue> steps, IReadOnlyList<DailyValue> distance, DateOnly today, int? stepsGoal)`
    - `WeekWorkouts CurrentWeek(IReadOnlyList<DailyValue> sessions, DateOnly today, int? goal)`
    - `WeightStats? Weight(IReadOnlyList<DailyValue> weights, DateOnly today, double? targetKg)`
    - `IReadOnlyList<ChartPoint> StepsSeries(IReadOnlyList<DailyValue> steps, IReadOnlyList<DailyValue> distance, DateOnly from, DateOnly to, Period period)`
    - `IReadOnlyList<ChartPoint> WeightSeries(IReadOnlyList<DailyValue> weights)`
    - `IReadOnlyList<ChartPoint> WorkoutsPerWeek(IReadOnlyList<DailyValue> sessions, DateOnly from, DateOnly to)`
    - `IReadOnlyList<Workout> Latest(IReadOnlyList<Workout> workouts, int count = 5)` — más recientes primero.

- [ ] **Step 1: Tests que fallan:**
  - `RangeFor30DaysIncludesToday`: `Range(Days30, 2026-10-06)` → `(2026-09-07, 2026-10-06)`.
  - `MondayOfSunday`: `MondayOf(2026-10-11)` → `2026-10-05`.
  - `TodayComputesKmAndGoalPercent`: pasos 7840, distancia 5900 m, objetivo 10000 → `TodayStats(7840, 5.9, 78.4)`; sin objetivo → `StepsGoalPercent == null`; sin datos de hoy → `(0, 0, 0)` con objetivo.
  - `CurrentWeekCountsSessionsSinceMonday`: sesiones (Count) el lunes 05/10 (1), el 06/10 (2) y el domingo 04/10 (1), hoy 06/10, objetivo 4 → `WeekWorkouts(3, 4)`.
  - `WeightUsesLatestAndChangeOver30Days`: pesos 2026-09-05 = 87.2, 2026-09-20 = 86.8, 2026-10-06 = 86.4, objetivo 82 → `CurrentKg 86.4`, `ChangeOver30Days -0.8` (referencia: el último en o antes de `today-30` = 09-05), `DistanceToTarget 4.4`.
  - `WeightWithoutOldMeasurementHasNoChange`: un solo peso → `ChangeOver30Days == null`; sin pesos → `Weight(...) == null`.
  - `StepsSeriesFillsMissingDaysWithZero`: 30 días con datos en 2 → 30 puntos, 28 con `Value 0`; `Tooltip` incluye pasos y km.
  - `StepsSeriesForYearIsWeeklyAverage`: semana con 7000 y 14000 en dos días → valor `3000` (21000/7) para esa semana; etiquetas por semana.
  - `WorkoutsPerWeekGroupsByMonday`: sesiones repartidas → un punto por semana del rango (semanas sin sesiones con 0).
  - `EmptyInputsProduceEmptyOrZeroSeries`: listas vacías no lanzan.
  - `LatestReturnsFiveMostRecentFirst`.
- [ ] **Step 2:** `dotnet test DashBoard.sln` → falla.
- [ ] **Step 3:** Implementar. Formato de etiquetas y tooltips con `CultureInfo("es-ES")` (`"dd/MM"`, números `N0`, km con un decimal).
- [ ] **Step 4:** `dotnet test DashBoard.sln` → PASS.
- [ ] **Step 5: Commit**: `feat: añadir cálculos de resumen del módulo Salud`

### Task 6: Gráficas SVG

**Files:**
- Create: `src/Modules/DashBoard.Modules.Salud/Charts/ChartGeometry.cs`, `Charts/BarChart.razor`, `Charts/LineChart.razor`
- Test: `tests/DashBoard.Modules.Salud.Tests/ChartGeometryTests.cs`

**Interfaces:**
- Consumes: `ChartPoint` (Task 5).
- Produces (namespace `DashBoard.Modules.Salud.Charts`):
  - `public sealed record ChartScale(double Min, double Max);`
  - `public static class ChartGeometry`:
    - `ChartScale BarScale(IReadOnlyList<double> values, double? goal)` — `Min 0`, `Max = max(valores, objetivo)`; si es 0 → `1`.
    - `ChartScale LineScale(IReadOnlyList<double> values, double? goal)` — mínimo y máximo de valores y objetivo con un 5 % de margen; un único valor `v` → `(v-1, v+1)`; vacío → `(0, 1)`.
    - `double Y(double value, ChartScale scale, double height)` — 0 abajo (`height`), máximo arriba (`0`).
    - `double BarWidth(int count, double width)` y `double X(int index, int count, double width)`.
  - `BarChart` y `LineChart`: parámetros `[Parameter] IReadOnlyList<ChartPoint> Points`, `[Parameter] double? Goal`; con `Points` vacío pintan el texto "Sin datos"; cada barra/punto lleva `<title>@point.Tooltip</title>`; línea de objetivo discontinua (`stroke-dasharray`); colores con `var(--bs-primary)` y `var(--bs-secondary-color)`; `viewBox` fijo y `width="100%"`.

- [ ] **Step 1: Tests que fallan:** `BarScaleStartsAtZeroAndIncludesGoal` (valores 5000, 8000; objetivo 10000 → `(0, 10000)`), `BarScaleWithAllZerosAvoidsDivisionByZero` (→ `Max 1`), `LineScaleSinglePoint` (86.4 → `(85.4, 87.4)`), `LineScaleEmpty` (→ `(0, 1)`), `LineScaleIncludesGoalWithMargin` (valores 86–87, objetivo 82 → `Min < 82`, `Max > 87`), `YMapsMinToBottomAndMaxToTop`, `XAndBarWidthSpreadEvenly` (4 barras en 400 → ancho < 100, X(0) ≥ 0, X(3) + ancho ≤ 400).
- [ ] **Step 2:** `dotnet test DashBoard.sln` → falla.
- [ ] **Step 3:** Implementar `ChartGeometry` y los dos componentes.
- [ ] **Step 4:** `dotnet test DashBoard.sln` → PASS.
- [ ] **Step 5: Commit**: `feat: añadir gráficas SVG del módulo Salud`

### Task 7: Página `/salud`

**Files:**
- Modify: `src/Modules/DashBoard.Modules.Salud/Pages/SaludPage.razor`
- Create (si hace falta separar lógica de estado): `src/Modules/DashBoard.Modules.Salud/Pages/SaludMessages.cs` con los textos literales de Global Constraints.

**Interfaces:**
- Consumes: `SaludApiClient`, `SaludClock`, `SaludOptions`, `TimeProvider`, `TimeZoneInfo` (Task 4); `SaludSummary`, `Period` (Task 5); `BarChart`, `LineChart` (Task 6).

- [ ] **Step 1: Implementar la página** según el boceto del spec §4.5:
  - `OnInitializedAsync`: en paralelo (`Task.WhenAll`) — pasos y distancia de hoy, sesiones desde `MondayOf(today)`, pesos de `today-365..today`, entrenos desde `today-90 días` (UTC); y la evolución del periodo por defecto (`Days30`).
  - Botones 30 días / 90 días / 1 año: recargan pasos, distancia, pesos y sesiones del periodo (`from` de sesiones = `MondayOf(from)`), con indicador de carga.
  - Si alguna llamada devuelve `NotConfigured`, `Unreachable` o `Unauthorized`, se muestra solo el mensaje correspondiente (prioridad en ese orden); `InvalidResponse` se trata como `Unreachable`.
  - Tarjetas: pasos hoy + barra de progreso (si hay objetivo), km hoy, entrenos `n / objetivo`, peso actual + variación 30 d + objetivo.
- [ ] **Step 2: Verificación manual en local** con SaludApi en Docker (Task 2) y DashBoard con `dotnet run --project src/DashBoard.Web` y variables `Salud__ApiUrl=http://localhost:8081`, `Salud__ApiKey=<query key>`: `/salud` muestra datos reales; cambiar de periodo funciona; con `Salud__ApiKey=mala` aparece el mensaje de clave rechazada.
- [ ] **Step 3:** `dotnet test DashBoard.sln` → PASS.
- [ ] **Step 4: Commit**: `feat: añadir la página del módulo Salud`

### Task 8: Docker, configuración y documentación del DashBoard

**Files:**
- Modify: `docker-compose.yml`, `.gitignore` (añadir `.env`), `CLAUDE.md`
- Create: `.env.example`, `.env` (no versionado; `SALUD_API_KEY` = `SALUDAPI_QUERY_API_KEY` de `../SaludApi/.env`)

- [ ] **Step 1: Configuración**
  - `docker-compose.yml`: `dashboard` con `networks: [default, homelab]`, `homelab` externa (como en Task 2), y entorno `Salud__ApiUrl=http://saludapi:8080`, `Salud__ApiKey=${SALUD_API_KEY}`, `Salud__StepsPerDay=${SALUD_STEPS_PER_DAY:-}`, `Salud__WorkoutsPerWeek=${SALUD_WORKOUTS_PER_WEEK:-}`, `Salud__TargetWeightKg=${SALUD_TARGET_WEIGHT_KG:-}`. Comprobar que una variable vacía deja el objetivo a `null` (si el binder falla con cadena vacía, omitir las líneas y documentarlas como opcionales en `.env.example`).
  - `.env.example`: las cuatro variables con comentarios.
  - `CLAUDE.md`: sección del módulo Salud (configuración, red `homelab`, dependencia de SaludApi).
- [ ] **Step 2: Verificación de extremo a extremo**

Run: `docker compose up --build -d` en DashBoard (SaludApi ya levantada en `homelab`).
Expected: `http://localhost:8080/salud` muestra pasos, km, peso y entrenamientos reales; Inicio muestra la tarjeta "Salud".
Run: `docker compose stop saludapi` en SaludApi → `/salud` muestra "No se puede conectar con SaludApi." y `/sistema` sigue funcionando; después `docker compose start saludapi`.
Run: `dotnet test DashBoard.sln` → PASS.

- [ ] **Step 3: Commit**: `feat: conectar el DashBoard con SaludApi mediante la red homelab`

- [ ] **Step 4: Cierre:** `superpowers:finishing-a-development-branch` en los dos repos (merge de `feature/resumen-diario` a `master` en SaludApi y de `feature/modulo-salud` a `main` en DashBoard, con confirmación; push solo si el usuario lo pide).
