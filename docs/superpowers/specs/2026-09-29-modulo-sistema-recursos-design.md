# Infraestructura de módulos + módulo Sistema (recursos de la máquina)

- **Fecha:** 2026-09-29
- **Estado:** aprobado en conversación, pendiente de revisión del spec escrito
- **Rama:** `feature/modulo-sistema-recursos`

## 1. Objetivo

Primera de tres entregas del módulo Sistema / Homelab:

1. **Esta entrega:** infraestructura de módulos en DashBoard + módulo Sistema
   con los recursos de la máquina (CPU, RAM, disco, uptime, temperatura).
2. Contenedores Docker (spec aparte).
3. Estado de servicios por HTTP (spec aparte).

Esta entrega fija la plantilla que seguirán todos los módulos futuros: un
módulo se registra una vez y aparece automáticamente como tarjeta en Inicio
y como entrada en la navegación lateral.

### Requisitos del usuario

- Ver CPU, RAM, disco, tiempo encendida y temperatura de la máquina.
- Valores en vivo, refrescados automáticamente mientras la página está
  abierta. Sin historial ni persistencia.
- Arquitectura de módulos independientes según `CLAUDE.md`.

### Fuera de alcance

- Historial de métricas y gráficas.
- Contenedores Docker y estado de servicios (entregas 2 y 3).
- NAS.
- Tests automáticos de componentes Blazor (bUnit).

## 2. Estructura de proyectos

El proyecto actual `DashBoard.Core` es a la vez la aplicación web y lo que
los módulos deben referenciar, lo que provocaría una referencia circular
(Web → módulo → Core = Web). Se separa en dos:

```
src/
  DashBoard.Web/                     aplicación: Program.cs, layout, Inicio (el actual DashBoard.Core renombrado con git mv)
  DashBoard.Core/                    biblioteca: contrato de módulo (nuevo)
  Modules/
    DashBoard.Modules.Sistema/       Razor class library: página /sistema + lectura de métricas (nuevo)
tests/
  DashBoard.Architecture.Tests/      reglas de referencias entre proyectos (nuevo)
  DashBoard.Modules.Sistema.Tests/   tests del módulo (nuevo)
```

Reglas de referencias:

- `DashBoard.Web` → `DashBoard.Core` + cada módulo.
- `DashBoard.Modules.*` → solo `DashBoard.Core`.
- `DashBoard.Core` → ningún proyecto de la solución.

Namespaces: al renombrar, el namespace raíz de la aplicación pasa de
`DashBoard.Core` a `DashBoard.Web`.

## 3. Contrato de módulo (DashBoard.Core)

`DashBoard.Core` es una class library con referencia al framework
`Microsoft.AspNetCore.App` (para `IServiceCollection` / `IConfiguration`).

```csharp
namespace DashBoard.Core;

public interface IDashboardModule
{
    string Title { get; }        // "Sistema / Homelab"
    string Description { get; }  // texto de la tarjeta en Inicio
    string Icon { get; }         // clase de Bootstrap Icons, p. ej. "bi-hdd-network-fill"
    string Route { get; }        // ruta relativa sin barra inicial, p. ej. "sistema"
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
```

## 4. Registro de módulos (DashBoard.Web)

- `Program.cs` declara la lista explícita de módulos, una línea por módulo:
  `IDashboardModule[] modules = [new SistemaModule()];`
- Para cada módulo: se llama a `ConfigureServices(builder.Services,
  builder.Configuration)` y se registra la instancia como singleton
  `IDashboardModule`.
- `ModuleCatalog` (clase de Web, singleton) guarda la lista de módulos y
  expone `Assemblies` (los ensamblados distintos de los módulos).
- Los ensamblados de los módulos se añaden al enrutado:
  - `app.MapRazorComponents<App>().AddAdditionalAssemblies(catalog.Assemblies)`.
  - `Routes.razor` inyecta `ModuleCatalog` y pasa `Assemblies` a
    `AdditionalAssemblies` del `Router`.
- `Home.razor` y `NavMenu.razor` inyectan `IEnumerable<IDashboardModule>` y
  generan tarjetas y enlaces a partir de él. Se eliminan las listas escritas
  a mano.
- Se eliminan `Pages/Sistema.razor` y `Pages/Proyectos.razor` (placeholders).
  Proyectos volverá cuando exista su módulo.

Añadir un módulo futuro = crear proyecto en `src/Modules/`, referenciarlo
desde Web y añadir una línea a la lista de `Program.cs`.

## 5. Módulo Sistema

### 5.1 Configuración

`SistemaOptions` (sección `Sistema` de configuración):

| Propiedad | Por defecto | Uso |
|---|---|---|
| `ProcPath` | `/proc` | origen de `stat`, `meminfo`, `uptime` |
| `SysPath` | `/sys` | origen de `class/thermal/thermal_zone*/temp` |
| `DiskPath` | `/` | ruta para `DriveInfo` |
| `RefreshInterval` | `00:00:02` | periodo de lectura |

### 5.2 Parsers (funciones puras, reciben texto)

| Parser | Entrada | Salida |
|---|---|---|
| `CpuStatParser` | primera línea `cpu` de `/proc/stat` | ticks totales y en reposo (idle + iowait) |
| `MemInfoParser` | `/proc/meminfo` | `MemTotal` y `MemAvailable` (bytes); usada = total − disponible |
| `UptimeParser` | `/proc/uptime` | `TimeSpan` desde el primer número |
| `ThermalParser` | contenido de un `temp` (miligrados) | grados °C |

- **% CPU** = `1 − Δidle / Δtotal` entre dos lecturas consecutivas de
  `/proc/stat`. Sin lectura previa (primer ciclo) el valor es `null`.
- **Temperatura**: se leen todas las zonas `thermal_zone*` y se muestra la
  máxima. Zonas ilegibles se ignoran; si no queda ninguna, `null`.
- **Disco**: `DriveInfo(DiskPath)` → `TotalSize` y `AvailableFreeSpace`.
- Entradas vacías o con formato inesperado → el parser indica fallo (sin
  excepción no controlada hacia arriba).

### 5.3 Muestreo: `HostMetricsSampler`

- `BackgroundService` registrado como singleton (instancia única para toda
  la aplicación, compartida por todas las pestañas).
- Cada `RefreshInterval` lee todas las fuentes y publica un snapshot
  inmutable:

  ```csharp
  public sealed record HostMetrics(
      double? CpuPercent,
      UsageBytes? Memory,       // Used, Total
      UsageBytes? Disk,         // Used, Total
      TimeSpan? Uptime,
      double? TemperatureCelsius,
      DateTimeOffset Timestamp);
  ```

- Expone `HostMetrics? Current` y el evento `Updated`.
- Cada métrica se lee de forma independiente: un fallo deja esa métrica a
  `null` sin afectar a las demás.
- Cada fallo se registra en el log una única vez por métrica (no en cada
  ciclo); si la métrica se recupera y vuelve a fallar, se vuelve a registrar.

### 5.4 Página `/sistema`

- Componente interactivo (Interactive Server) del módulo.
- Al inicializarse se suscribe a `Updated`; en cada evento llama a
  `InvokeAsync(StateHasChanged)`. Implementa `IDisposable` para desuscribirse.
- Cinco tarjetas Bootstrap coherentes con el estilo existente:
  - **CPU**: porcentaje + barra de progreso.
  - **RAM**: usada / total en GB + barra.
  - **Disco**: usado / total en GB + barra.
  - **Temperatura**: °C; clase de aviso ≥ 70 °C, peligro ≥ 85 °C.
  - **Uptime**: formato `3 d 4 h 12 min`.
- Métrica `null` → la tarjeta muestra "No disponible".

## 6. Docker

`docker-compose.yml` añade montajes de solo lectura y variables de entorno:

```yaml
volumes:
  - dataprotection-keys:/keys
  - /proc:/host/proc:ro
  - /sys:/host/sys:ro
  - /:/host/root:ro
environment:
  - Sistema__ProcPath=/host/proc
  - Sistema__SysPath=/host/sys
  - Sistema__DiskPath=/host/root
```

- En Docker Desktop (Windows) los datos son los de la VM WSL2, no los de
  Windows. En la OrangePi serán los reales.
- `Dockerfile`: rutas actualizadas a `DashBoard.Web`; se copian los
  `.csproj` de Web, Core y módulos antes de `dotnet restore` para cachear la
  capa. Imágenes base sin cambios (multi-arquitectura, ARM64 incluido).

## 7. Tests (xUnit)

**`DashBoard.Modules.Sistema.Tests`**

- Cada parser con fixtures reales de `/proc` y `/sys` (incluida una máquina
  ARM con varias zonas térmicas) y casos de texto vacío / malformado.
- Cálculo de % CPU a partir de dos lecturas.
- `HostMetricsSampler` apuntando (vía `SistemaOptions`) a un directorio
  temporal con archivos falsos: métricas correctas, y una fuente rota deja
  solo esa métrica a `null`.

**`DashBoard.Architecture.Tests`**

- Localiza la raíz del repositorio subiendo directorios desde el directorio
  de ejecución hasta encontrar `DashBoard.sln`.
- Recorre `src/Modules/*/*.csproj` y falla si:
  - un módulo tiene una `ProjectReference` distinta de `DashBoard.Core`;
  - un proyecto en `src/Modules/` no se llama `DashBoard.Modules.*`.
- Falla si `DashBoard.Core.csproj` tiene alguna `ProjectReference`.
- La lógica de comprobación se prueba también contra un `.csproj` de ejemplo
  que incumple la regla, para asegurar que detecta el fallo.
- Cubre módulos futuros sin modificar el test.

## 8. Criterios de terminado

1. `dotnet build` y `dotnet test` pasan.
2. `dotnet run` en Windows: Inicio muestra la tarjeta de Sistema generada
   desde el registro y `/sistema` muestra "No disponible" sin errores.
3. `docker compose up --build` (lo ejecuta el usuario): `/sistema` muestra
   valores reales que se refrescan cada 2 s.
4. `CLAUDE.md` actualizado: papel de Web vs Core y pasos para añadir un
   módulo.
