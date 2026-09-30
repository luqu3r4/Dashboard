# Módulo Sistema — Entrega 2: contenedores Docker — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Añadir a `/sistema` una sección "Contenedores" de solo lectura con estado, tiempo activo, CPU y RAM de cada contenedor Docker, refrescada en vivo.

**Architecture:** Todo vive en el módulo Sistema, carpeta `Containers/`. Un `DockerApiClient` consulta por HTTP un `docker-socket-proxy` (solo lectura); lectores JSON puros convierten las respuestas; `ContainerMetricsReader` (con estado: lecturas previas de CPU y fallos ya registrados) compone un `ContainersSnapshot`; `ContainerMetricsSampler` (BackgroundService, separado de `HostMetricsSampler`) lo publica; `SistemaPage` lo pinta en una tabla. Es el mismo reparto lector/muestreador de la entrega 1.

**Tech Stack:** .NET 10, Blazor Server, `System.Net.Http`, `System.Text.Json`, xUnit, Docker Compose, `tecnativa/docker-socket-proxy:v0.4.1`.

**Spec:** `docs/superpowers/specs/2026-09-30-modulo-sistema-contenedores-design.md`

## Global Constraints

- Solo lectura: nada en el código ni en el proxy puede arrancar/parar/modificar contenedores. Proxy con `CONTAINERS=1` y nada más.
- El DashBoard **no** monta `/var/run/docker.sock`; solo lo monta `docker-proxy`, que no publica puertos.
- Sin dependencias NuGet nuevas en el módulo (tampoco en los tests: los dobles de prueba se escriben a mano).
- El módulo solo referencia `DashBoard.Core` (lo comprueba `DashBoard.Architecture.Tests`).
- Namespace `DashBoard.Modules.Sistema.Containers`, ficheros en `src/Modules/DashBoard.Modules.Sistema/Containers/`.
- `DockerApiUrl` vacío o en blanco = sección desactivada: sin peticiones HTTP ni logs.
- Refresco cada `SistemaOptions.RefreshInterval` (≤ 0 → 2 s); timeout HTTP de 2 s por petición.
- Textos de la UI en español, tal cual en el spec: "Contenedores", "No disponible", "No hay contenedores", "En marcha", "Parado", "Reiniciando", "En pausa", `—`.
- Imágenes multi-arquitectura (`linux/arm64` y `linux/amd64`).
- Commits: Conventional Commits, prefijo en inglés y descripción en español.

## Review Focus

1. `State.StartedAt` llega con **nanosegundos** (`2026-09-30T08:15:42.123456789Z`, 9 decimales) y `DateTimeOffset.Parse` no acepta más de 7 → debe leerse igualmente (test en Tarea 1).
2. `inactive_file` mayor que `usage` (ocurre justo tras arrancar o con cachés compartidas) → memoria usada 0, nunca negativa (test en Tarea 1).
3. Un contenedor desaparece entre la lista y sus estadísticas (`404`) → solo ese contenedor sin métricas; la lista sigue visible (test en Tarea 3).
4. El proxy cae y vuelve → tras un ciclo con "No disponible", el siguiente ciclo correcto vuelve a mostrar la lista (test en Tarea 3).
5. `StartedAt` en el futuro respecto al reloj del DashBoard (relojes desfasados) o `0001-01-01T00:00:00Z` → tiempo activo `null`, no un valor negativo ni absurdo (test en Tarea 3).

---

### Task 1: Lectores de JSON de la API de Docker

**Files:**
- Create: `src/Modules/DashBoard.Modules.Sistema/Containers/ContainerSummary.cs`
- Create: `src/Modules/DashBoard.Modules.Sistema/Containers/ContainerStats.cs`
- Create: `src/Modules/DashBoard.Modules.Sistema/Containers/DockerJsonReader.cs`
- Create: `tests/DashBoard.Modules.Sistema.Tests/Fixtures/docker-list.json`, `docker-inspect.json`, `docker-stats-cgroupv2.json`, `docker-stats-cgroupv1.json`
- Modify: `tests/DashBoard.Modules.Sistema.Tests/DashBoard.Modules.Sistema.Tests.csproj` (copiar `Fixtures\*.json` a la salida, igual que `*.txt`)
- Test: `tests/DashBoard.Modules.Sistema.Tests/DockerJsonReaderTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record ContainerSummary(string Id, string Name, string Image, string State);`
  - `public readonly record struct ContainerStats(ulong CpuTotalUsage, ulong SystemCpuUsage, int OnlineCpus, UsageBytes? Memory);` (`UsageBytes` ya existe en `DashBoard.Modules.Sistema.Metrics`)
  - `public static class DockerJsonReader` con:
    - `static bool TryReadList(string json, out IReadOnlyList<ContainerSummary> containers)`
    - `static bool TryReadStartedAt(string json, out DateTimeOffset startedAt)`
    - `static bool TryReadStats(string json, out ContainerStats stats)`
  - Todos devuelven `false` (nunca lanzan) ante JSON malformado o campos obligatorios ausentes.

Fixtures (forma real de la API, recortada a los campos usados; pueden llevar campos extra):

- `docker-list.json`: array con tres contenedores:
  `{"Id":"aaa111","Names":["/dashboard-dashboard-1"],"Image":"dashboard-dashboard","State":"running"}`,
  `{"Id":"bbb222","Names":["/viejo"],"Image":"nginx:1.27","State":"exited"}`,
  `{"Id":"ccc333","Names":["/inestable"],"Image":"redis:7","State":"restarting"}`.
- `docker-inspect.json`: `{"Id":"aaa111","State":{"Status":"running","StartedAt":"2026-09-30T08:15:42.123456789Z"}}`.
- `docker-stats-cgroupv2.json`: `cpu_stats.cpu_usage.total_usage=2000000000`, `cpu_stats.system_cpu_usage=100000000000`, `cpu_stats.online_cpus=4`, `memory_stats.usage=209715200`, `memory_stats.limit=8589934592`, `memory_stats.stats.inactive_file=52428800`.
- `docker-stats-cgroupv1.json`: mismos `cpu_stats`; `memory_stats.usage=209715200`, `memory_stats.limit=2147483648`, `memory_stats.stats.total_inactive_file=104857600`.

Reglas de lectura:
- `Name` = primer elemento de `Names` sin la `/` inicial; si `Names` falta o está vacío, los 12 primeros caracteres de `Id`.
- `Id`, `Image` y `State` obligatorios en cada elemento; si falta alguno, `TryReadList` devuelve `false`.
- `StartedAt`: truncar la parte fraccionaria a 7 dígitos antes de parsear con `CultureInfo.InvariantCulture` y `DateTimeStyles.AssumeUniversal`.
- Stats: `cpu_stats.cpu_usage.total_usage`, `cpu_stats.system_cpu_usage` y `cpu_stats.online_cpus` (> 0) obligatorios. Memoria: usada = `usage` − (`inactive_file` ?? `total_inactive_file` ?? 0), mínimo 0; total = `limit`. Si falta `memory_stats.usage` o `limit`, `Memory = null` pero la lectura sigue siendo válida (`true`).

- [ ] **Step 1: Crear las fixtures y añadir al `.csproj` de tests** `<None Include="Fixtures\*.json" CopyToOutputDirectory="PreserveNewest" />`.

- [ ] **Step 2: Escribir los tests que fallan** en `DockerJsonReaderTests.cs` (leer fixtures con `File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", ...))`, como en `ParserTests.cs`):

```csharp
[Fact] public void Lista_lee_los_tres_contenedores()
// TryReadList(docker-list.json) == true; Count == 3;
// [0] == new ContainerSummary("aaa111", "dashboard-dashboard-1", "dashboard-dashboard", "running");
// [1].State == "exited"; [2].State == "restarting"

[Fact] public void Lista_sin_nombres_usa_el_id_corto()
// json: [{"Id":"0123456789abcdef","Names":[],"Image":"x","State":"running"}] → Name == "0123456789ab"

[Fact] public void Lista_vacia_es_valida()          // "[]" → true, Count == 0

[Fact] public void Detalle_lee_StartedAt_con_nanosegundos()
// TryReadStartedAt(docker-inspect.json) == true;
// startedAt == new DateTimeOffset(2026, 9, 30, 8, 15, 42, TimeSpan.Zero).AddTicks(1234567)

[Fact] public void Stats_cgroup_v2()
// true; CpuTotalUsage == 2000000000; SystemCpuUsage == 100000000000; OnlineCpus == 4;
// Memory == new UsageBytes(157286400, 8589934592)

[Fact] public void Stats_cgroup_v1()
// Memory == new UsageBytes(104857600, 2147483648)

[Fact] public void Stats_cache_mayor_que_uso_da_cero()
// v2 con usage=1000, inactive_file=5000 → Memory!.Used == 0

[Fact] public void Stats_sin_memoria_mantiene_cpu()
// "memory_stats":{} → true; Memory == null; OnlineCpus == 4

[Theory]
[InlineData("")] [InlineData("{")] [InlineData("null")] [InlineData("{\"cpu_stats\":{}}")]
public void Stats_invalidas_devuelven_false(string json)

[Theory]
[InlineData("")] [InlineData("{}")] [InlineData("[{\"Id\":\"a\"}]")]
public void Lista_invalida_devuelve_false(string json)

[Theory]
[InlineData("")] [InlineData("{}")] [InlineData("{\"State\":{\"StartedAt\":\"ayer\"}}")]
public void Detalle_invalido_devuelve_false(string json)
```

- [ ] **Step 3: Ejecutar y ver que falla**

Run: `dotnet test tests/DashBoard.Modules.Sistema.Tests --filter DockerJsonReaderTests`
Expected: error de compilación (`DockerJsonReader` no existe).

- [ ] **Step 4: Implementar los records y `DockerJsonReader`** con `JsonDocument.Parse` dentro de `try/catch (JsonException)`; comprobar `ValueKind` antes de cada acceso (`TryGetProperty`, `TryGetUInt64`, `TryGetInt64`).

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test tests/DashBoard.Modules.Sistema.Tests --filter DockerJsonReaderTests`
Expected: todos PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Modules/DashBoard.Modules.Sistema/Containers tests/DashBoard.Modules.Sistema.Tests
git commit -m "feat: leer la lista, el detalle y las estadísticas de la API de Docker"
```

---

### Task 2: Cálculo de CPU % por contenedor

**Files:**
- Create: `src/Modules/DashBoard.Modules.Sistema/Containers/ContainerCpu.cs`
- Test: `tests/DashBoard.Modules.Sistema.Tests/ContainerCpuTests.cs`

**Interfaces:**
- Consumes: `ContainerStats` (Tarea 1).
- Produces: `public static class ContainerCpu { public static double? Percent(ContainerStats previous, ContainerStats current); }`

Fórmula del spec: `cpu% = (Δtotal_usage / Δsystem_cpu_usage) × online_cpus × 100`, con `online_cpus` de la lectura actual. `null` si `Δsystem ≤ 0` o `current.CpuTotalUsage < previous.CpuTotalUsage`. Resultado limitado a `[0, 100 × online_cpus]`. Cuidado: los contadores son `ulong`; restar solo tras comprobar el orden.

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
static ContainerStats S(ulong total, ulong system, int cpus = 4) => new(total, system, cpus, null);

[Fact] public void Dos_lecturas_dan_el_porcentaje()
// Percent(S(1_000_000_000, 100_000_000_000), S(2_000_000_000, 110_000_000_000)) == 40.0 (precisión 6)

[Fact] public void Sistema_sin_avance_da_null()
// Percent(S(1, 100), S(2, 100)) == null

[Fact] public void Contador_reiniciado_da_null()
// Percent(S(5_000, 100), S(1_000, 200)) == null

[Fact] public void Se_limita_a_100_por_cpu()
// Percent(S(0, 0), S(20_000_000_000, 10_000_000_000)) == 400.0

[Fact] public void Sin_uso_da_cero()
// Percent(S(1_000, 100), S(1_000, 200)) == 0.0
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test tests/DashBoard.Modules.Sistema.Tests --filter ContainerCpuTests`
Expected: error de compilación.

- [ ] **Step 3: Implementar `ContainerCpu.Percent`.**

- [ ] **Step 4: Ejecutar y ver que pasa** (mismo comando, todos PASS).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/DashBoard.Modules.Sistema/Containers/ContainerCpu.cs tests/DashBoard.Modules.Sistema.Tests/ContainerCpuTests.cs
git commit -m "feat: calcular el uso de CPU de cada contenedor"
```

---

### Task 3: Cliente HTTP y lector de métricas de contenedores

**Files:**
- Modify: `src/Modules/DashBoard.Modules.Sistema/SistemaOptions.cs` (añadir `DockerApiUrl`)
- Create: `src/Modules/DashBoard.Modules.Sistema/Containers/DockerApiClient.cs`
- Create: `src/Modules/DashBoard.Modules.Sistema/Containers/ContainersSnapshot.cs` (ambos records)
- Create: `src/Modules/DashBoard.Modules.Sistema/Containers/ContainerMetricsReader.cs`
- Test: `tests/DashBoard.Modules.Sistema.Tests/FakeDockerHandler.cs`, `tests/DashBoard.Modules.Sistema.Tests/ContainerMetricsReaderTests.cs`

**Interfaces:**
- Consumes: `DockerJsonReader`, `ContainerSummary`, `ContainerStats` (Tarea 1); `ContainerCpu.Percent` (Tarea 2); `UsageBytes`.
- Produces:
  - `SistemaOptions.DockerApiUrl`: `public string? DockerApiUrl { get; set; }` (por defecto `null`).
  - `public sealed class DockerApiClient(HttpClient http)` con `Task<string> GetContainersAsync(CancellationToken ct)` (`GET /containers/json?all=true`), `Task<string> GetContainerAsync(string id, CancellationToken ct)` (`GET /containers/{id}/json`), `Task<string> GetStatsAsync(string id, CancellationToken ct)` (`GET /containers/{id}/stats?stream=false&one-shot=true`). Lanzan `HttpRequestException` si el código no es de éxito (`EnsureSuccessStatusCode`).
  - Records exactamente como en el spec:
    `public sealed record ContainersSnapshot(IReadOnlyList<ContainerInfo>? Containers, DateTimeOffset Timestamp);`
    `public sealed record ContainerInfo(string Id, string Name, string Image, string State, TimeSpan? Uptime, double? CpuPercent, UsageBytes? Memory);`
  - `public sealed class ContainerMetricsReader(DockerApiClient client, IOptions<SistemaOptions> options, ILogger<ContainerMetricsReader> logger, TimeProvider timeProvider)` con `Task<ContainersSnapshot> ReadAsync(CancellationToken ct)`. Con estado y no seguro entre hilos (solo lo llama el bucle del sampler), igual que `HostMetricsReader`.

Comportamiento de `ReadAsync`:
- `DockerApiUrl` nulo o en blanco → `Containers = null`, sin llamar al cliente ni registrar logs.
- Falla la lista (excepción HTTP, timeout, `TryReadList == false`) → `Containers = null`.
- Para cada contenedor con `State == "running"`, detalle y estadísticas en paralelo (`Task.WhenAll` sobre todos los contenedores; cada uno con sus dos peticiones). Los fallos de uno solo anulan sus campos.
- Tras `WhenAll`, en secuencia: CPU con `ContainerCpu.Percent` contra la lectura previa del mismo `Id` (diccionario privado); guardar la nueva; descartar del diccionario los `Id` que ya no están en la lista.
- `Uptime = now − StartedAt`; si es negativo, o `StartedAt.Year <= 1`, → `null`.
- Contenedores no `running`: `Uptime`, `CpuPercent`, `Memory` = `null`.
- Logs: `LogWarning` una sola vez por tipo de fallo (`"lista"`, `"detalle"`, `"estadisticas"`) mientras dure, con un `HashSet<string>` como `HostMetricsReader.Guard`; un tipo deja de estar en fallo cuando un ciclo lo completa sin errores de ese tipo. La cancelación (`ct` cancelado) se propaga, no se registra.

Registro del `HttpClient` (se hace en la Tarea 4, pero se decide aquí): `DockerApiClient` es singleton porque lo usa un singleton; en lugar de `AddHttpClient` (que en un singleton retendría el handler para siempre) se crea con `new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(1) }) { Timeout = TimeSpan.FromSeconds(2) }`, que es la pauta de Microsoft para clientes de larga duración. Desviación consciente del spec §3.2, mismo resultado.

Dobles de prueba:
- `FakeDockerHandler : HttpMessageHandler` con un `Func<HttpRequestMessage, HttpResponseMessage>` y una lista `Requests` de las rutas pedidas (`PathAndQuery`).
- `FixedTimeProvider : TimeProvider` que devuelve un `DateTimeOffset` fijo en `GetUtcNow()` (se puede declarar en el mismo fichero de tests).
- Reader de prueba: `new DockerApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://docker-proxy:2375") })`, `DockerApiUrl = "http://docker-proxy:2375"`, reloj fijo en `2026-09-30T10:15:42Z`, fixtures de la Tarea 1.

- [ ] **Step 1: Escribir los tests que fallan** en `ContainerMetricsReaderTests.cs`:

```csharp
[Fact] public async Task Todo_correcto_devuelve_la_lista_con_metricas()
// handler: /containers/json?all=true → docker-list.json; /containers/aaa111/json → docker-inspect.json;
// /containers/aaa111/stats?stream=false&one-shot=true → docker-stats-cgroupv2.json
// Containers.Count == 3; aaa111: Uptime ≈ 2 h (reloj − StartedAt), Memory == new UsageBytes(157286400, 8589934592),
// CpuPercent == null (primera lectura); bbb222 y ccc333: Uptime/CpuPercent/Memory null;
// no se pidieron detalle ni stats de bbb222 ni de ccc333

[Fact] public async Task Segunda_lectura_calcula_la_cpu()
// stats devuelve primero la fixture v2 y después la misma con total_usage=3000000000 y system_cpu_usage=110000000000
// segunda ReadAsync → aaa111.CpuPercent == 40.0 (precisión 6)

[Fact] public async Task Si_falla_la_lista_no_hay_contenedores()
// lista → 500 → Containers == null

[Fact] public async Task Tras_caer_el_proxy_la_lista_vuelve()
// 1.ª lista → 500, 2.ª → docker-list.json → Containers null y luego Count == 3

[Fact] public async Task Si_fallan_las_estadisticas_solo_ese_contenedor_pierde_metricas()
// stats → 404 (contenedor desaparecido); detalle OK
// Containers.Count == 3; aaa111.CpuPercent == null, Memory == null, Uptime != null

[Fact] public async Task Sin_url_no_hace_peticiones()
// DockerApiUrl = "  " → Containers == null; handler.Requests vacío

[Theory]
[InlineData("2026-09-30T11:00:00Z")]      // posterior al reloj
[InlineData("0001-01-01T00:00:00Z")]      // nunca arrancado
public async Task StartedAt_imposible_da_uptime_null(string startedAt)
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test tests/DashBoard.Modules.Sistema.Tests --filter ContainerMetricsReaderTests`
Expected: error de compilación.

- [ ] **Step 3: Implementar** `SistemaOptions.DockerApiUrl`, `DockerApiClient`, los records y `ContainerMetricsReader` según las reglas anteriores.

- [ ] **Step 4: Ejecutar y ver que pasa** (mismo comando, todos PASS), y después `dotnet test DashBoard.sln` completo sin fallos.

- [ ] **Step 5: Commit**

```bash
git add src/Modules/DashBoard.Modules.Sistema tests/DashBoard.Modules.Sistema.Tests
git commit -m "feat: leer las métricas de los contenedores desde la API de Docker"
```

---

### Task 4: Muestreador en segundo plano y registro en el módulo

**Files:**
- Create: `src/Modules/DashBoard.Modules.Sistema/Containers/ContainerMetricsSampler.cs`
- Modify: `src/Modules/DashBoard.Modules.Sistema/SistemaModule.cs`
- Test: `tests/DashBoard.Modules.Sistema.Tests/ContainerMetricsSamplerTests.cs`

**Interfaces:**
- Consumes: `ContainerMetricsReader.ReadAsync`, `ContainersSnapshot` (Tarea 3).
- Produces: `public sealed class ContainerMetricsSampler(ContainerMetricsReader reader, IOptions<SistemaOptions> options) : BackgroundService` con `ContainersSnapshot? Current` y `event Action? Updated`.

Mismo patrón que `HostMetricsSampler` (copiar su estructura): lectura inicial, `PeriodicTimer` con fallback a 2 s si el intervalo es ≤ 0, `Volatile` para `Current`, excepción inesperada del lector → se conserva el último snapshot, cada suscriptor de `Updated` invocado en su propio `try/catch`.

Registro en `SistemaModule.ConfigureServices`:
- `DockerApiClient` singleton construido como se decidió en la Tarea 3; `BaseAddress = new Uri(DockerApiUrl)` solo si `DockerApiUrl` no está en blanco (leer `IOptions<SistemaOptions>` desde el `IServiceProvider`).
- `ContainerMetricsReader` y `ContainerMetricsSampler` singleton; `AddHostedService(sp => sp.GetRequiredService<ContainerMetricsSampler>())`.

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
[Fact] public async Task Intervalo_negativo_no_detiene_el_arranque()
// Igual que HostMetricsSamplerTests: RefreshInterval = -1 s, DockerApiUrl = null.
// Tras StartAsync y 300 ms: ExecuteTask no IsFaulted; tras StopAsync: Current != null y Current.Containers == null

[Fact] public async Task Publica_la_lista_y_avisa_a_los_suscriptores()
// FakeDockerHandler de la Tarea 3 con las fixtures; un suscriptor que lanza y otro que marca un TaskCompletionSource.
// Tras StartAsync, el TaskCompletionSource se completa en < 2 s y Current.Containers.Count == 3
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test tests/DashBoard.Modules.Sistema.Tests --filter ContainerMetricsSamplerTests`
Expected: error de compilación.

- [ ] **Step 3: Implementar `ContainerMetricsSampler` y el registro en `SistemaModule`.**

- [ ] **Step 4: Ejecutar y ver que pasa**, luego `dotnet build DashBoard.sln` y `dotnet test DashBoard.sln` sin errores.

- [ ] **Step 5: Commit**

```bash
git add src/Modules/DashBoard.Modules.Sistema tests/DashBoard.Modules.Sistema.Tests
git commit -m "feat: muestrear los contenedores Docker en segundo plano"
```

---

### Task 5: Sección "Contenedores" en `/sistema`

**Files:**
- Modify: `src/Modules/DashBoard.Modules.Sistema/Pages/MetricsFormat.cs` (añadir `Memory`)
- Create: `src/Modules/DashBoard.Modules.Sistema/Pages/ContainerDisplay.cs`
- Modify: `src/Modules/DashBoard.Modules.Sistema/Pages/SistemaPage.razor`
- Modify: `src/Modules/DashBoard.Modules.Sistema/_Imports.razor` (`@using DashBoard.Modules.Sistema.Containers`)
- Test: `tests/DashBoard.Modules.Sistema.Tests/MetricsFormatTests.cs`, `tests/DashBoard.Modules.Sistema.Tests/ContainerDisplayTests.cs`

**Interfaces:**
- Consumes: `ContainerMetricsSampler.Current` / `Updated` (Tarea 4), `ContainerInfo` (Tarea 3), `MetricsFormat.Uptime` (existente).
- Produces:
  - `MetricsFormat.Memory(long bytes) -> string`: `< 1024³` → `"{bytes / 1024²:0} MB"`; si no, igual que `Gigabytes` (`"0.0 GB"`); cultura actual.
  - `public static class ContainerDisplay`:
    - `static (string Label, string CssClass) Badge(string state)`: `running` → ("En marcha", "text-bg-success"); `exited`, `created` → ("Parado", "text-bg-secondary"); `restarting` → ("Reiniciando", "text-bg-warning"); `paused` → ("En pausa", "text-bg-warning"); cualquier otro → (state, "text-bg-danger").
    - `static IReadOnlyList<ContainerInfo> Sort(IEnumerable<ContainerInfo> containers)`: primero `running`, después el resto; dentro de cada grupo por `Name` (`StringComparer.CurrentCultureIgnoreCase`).

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
// MetricsFormatTests (cultura es-ES, como Gigabytes_una_decimal)
[Theory]
[InlineData(157286400L, "150 MB")]
[InlineData(1073741823L, "1024 MB")]
[InlineData(1073741824L, "1,0 GB")]
[InlineData(8589934592L, "8,0 GB")]
public void Memory_MB_o_GB(long bytes, string esperado)

// ContainerDisplayTests
[Theory]
[InlineData("running", "En marcha", "text-bg-success")]
[InlineData("exited", "Parado", "text-bg-secondary")]
[InlineData("created", "Parado", "text-bg-secondary")]
[InlineData("restarting", "Reiniciando", "text-bg-warning")]
[InlineData("paused", "En pausa", "text-bg-warning")]
[InlineData("dead", "dead", "text-bg-danger")]
public void Badge_por_estado(string state, string label, string css)

[Fact] public void Sort_pone_primero_los_que_estan_en_marcha()
// entrada: ("zeta","exited"), ("beta","running"), ("alfa","exited"), ("Gamma","running")
// orden de Name: "beta", "Gamma", "alfa", "zeta"
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test tests/DashBoard.Modules.Sistema.Tests --filter "MetricsFormatTests|ContainerDisplayTests"`
Expected: error de compilación.

- [ ] **Step 3: Implementar `MetricsFormat.Memory` y `ContainerDisplay`.** Ejecutar el mismo comando: todos PASS.

- [ ] **Step 4: Añadir la sección a `SistemaPage.razor`** debajo del `div.row` de tarjetas:
  - `@inject ContainerMetricsSampler ContainerSampler`; suscribir `OnUpdated` también a `ContainerSampler.Updated` en `OnInitialized` y desuscribir en `Dispose`.
  - `<h2 class="h5 mt-5 mb-3">Contenedores</h2>`.
  - `Current?.Containers is null` → `<p class="text-secondary">No disponible</p>`; lista vacía → `<p class="text-secondary">No hay contenedores</p>`.
  - Si hay: `div.table-responsive` > `table.table.table-sm.align-middle` con cabeceras Nombre, Imagen, Estado, Tiempo activo, CPU, RAM; filas en el orden de `ContainerDisplay.Sort`; estado con `<span class="badge @css">@label</span>`; tiempo activo con `MetricsFormat.Uptime`; CPU `@cpu.ToString("0.0") %`; RAM `@MetricsFormat.Memory(used) / @MetricsFormat.Memory(total)`; cualquier `null` → `—`.

- [ ] **Step 5: Verificar en local**

Run: `dotnet build DashBoard.sln`, `dotnet test DashBoard.sln`, luego `dotnet run --project src/DashBoard.Web` y abrir `/sistema`.
Expected: compila, tests en verde, la sección "Contenedores" muestra "No disponible" (sin `DockerApiUrl`) y no hay errores en la consola del servidor.

- [ ] **Step 6: Commit**

```bash
git add src/Modules/DashBoard.Modules.Sistema tests/DashBoard.Modules.Sistema.Tests
git commit -m "feat: mostrar los contenedores Docker en la página Sistema"
```

---

### Task 6: Proxy en Docker Compose, documentación y verificación final

**Files:**
- Modify: `docker-compose.yml`
- Modify: `CLAUDE.md` (sección Stack)

**Interfaces:**
- Consumes: `Sistema__DockerApiUrl` → `SistemaOptions.DockerApiUrl` (Tarea 3).

- [ ] **Step 1: Añadir el servicio `docker-proxy`** a `docker-compose.yml` exactamente como en el spec §4 (imagen `tecnativa/docker-socket-proxy:v0.4.1`, `CONTAINERS=1`, socket `:ro`, `restart: unless-stopped`, sin `ports`), y en `dashboard`: `Sistema__DockerApiUrl=http://docker-proxy:2375` y `depends_on: [docker-proxy]`. Comentarios en español para principiante sobre: red interna de Compose y resolución por nombre de servicio, por qué el proxy no publica puertos, qué permite `CONTAINERS=1` (y que las escrituras quedan denegadas), y qué hace `depends_on` (orden de arranque, no espera a que esté listo).

- [ ] **Step 2: Actualizar `CLAUDE.md`**: una línea en Stack sobre el módulo Sistema consultando contenedores vía `docker-proxy` (solo lectura) y la variable `Sistema__DockerApiUrl`.

- [ ] **Step 3: Validar el compose sin desplegar**

Run: `docker compose config --quiet`
Expected: sin salida, código 0.

- [ ] **Step 4: Commit**

```bash
git add docker-compose.yml CLAUDE.md
git commit -m "feat: añadir docker-socket-proxy de solo lectura para el módulo Sistema"
```

- [ ] **Step 5: Verificación con Docker (la ejecuta el usuario; Claude explica cada comando)**
  1. `docker compose up --build` → en http://localhost:8080/sistema aparecen al menos `dashboard` y `docker-proxy`, "En marcha", con CPU y RAM refrescándose (la CPU aparece tras el segundo ciclo).
  2. Comprobar que el proxy rechaza escrituras:
     `docker run --rm --network dashboard_default curlimages/curl -s -o /dev/null -w "%{http_code}" -X POST http://docker-proxy:2375/containers/dashboard-dashboard-1/stop`
     Expected: `403`.
  3. `docker compose down`.
