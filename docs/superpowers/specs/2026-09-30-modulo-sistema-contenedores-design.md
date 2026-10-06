# Módulo Sistema — Entrega 2: contenedores Docker

- **Fecha:** 2026-09-30
- **Estado:** diseño aprobado en conversación, pendiente de revisión del spec escrito
- **Rama:** `feature/modulo-sistema-contenedores`
- **Entrega anterior:** `docs/superpowers/specs/2026-09-29-modulo-sistema-recursos-design.md`

## 1. Objetivo

Mostrar en `/sistema`, debajo de las tarjetas de la máquina, la lista de
contenedores Docker con su estado, tiempo activo, CPU y RAM, refrescada en
vivo.

### Requisitos del usuario

- **Solo lectura.** El DashBoard no tiene login; no puede arrancar, parar ni
  modificar contenedores.
- Ubicación: sección "Contenedores" en `/sistema`, debajo de las cinco
  tarjetas existentes.

### Supuestos aceptados

- Se muestran todos los contenedores (en marcha y parados).
- Refresco cada `RefreshInterval` (2 s por defecto), como el resto de la página.
- Si Docker no está accesible, la sección muestra "No disponible" y el resto
  de la página funciona igual.

### Fuera de alcance

- Acciones sobre contenedores (arrancar/parar/reiniciar) y login.
- Logs de contenedores.
- Historial de métricas.
- Estado de servicios por HTTP (entrega 3).

## 2. Acceso a Docker: socket proxy

Acceder a `/var/run/docker.sock` equivale a control total de Docker (≈ root
en la máquina); montarlo `:ro` no lo limita. Por eso el DashBoard **no**
monta el socket:

- Nuevo servicio `docker-proxy` con la imagen
  `tecnativa/docker-socket-proxy:v0.4.1` (multi-arquitectura, incluye
  `linux/arm64` y `linux/amd64`, verificado con `docker manifest inspect`).
- Solo el proxy monta el socket. Configuración `CONTAINERS=1`; el resto de
  secciones y todas las peticiones de escritura (`POST`, `DELETE`…) quedan
  denegadas por defecto.
- El proxy no publica puertos: solo es accesible desde la red interna de
  Compose, por el nombre `docker-proxy`.
- El DashBoard le consulta por HTTP (`http://docker-proxy:2375`).

## 3. Diseño en el módulo Sistema

Todo el código nuevo va en `src/Modules/DashBoard.Modules.Sistema/Containers/`
(namespace `DashBoard.Modules.Sistema.Containers`). Sin dependencias NuGet
nuevas (`HttpClient`/`IHttpClientFactory` y `System.Text.Json` vienen con
`Microsoft.AspNetCore.App`).

### 3.1 Configuración

`SistemaOptions` añade:

| Propiedad | Por defecto | Uso |
|---|---|---|
| `DockerApiUrl` | `null` (vacío) | URL base de la API de Docker (el proxy). Vacío = sección desactivada, "No disponible", sin peticiones ni logs de error. |

### 3.2 Cliente: `DockerApiClient`

`HttpClient` tipado (registrado con `AddHttpClient`), `BaseAddress =
DockerApiUrl`, timeout corto por petición (2 s). Tres llamadas:

| Método | Endpoint | Uso |
|---|---|---|
| lista | `GET /containers/json?all=true` | Id, nombres, imagen, estado de todos los contenedores |
| detalle | `GET /containers/{id}/json` | `State.StartedAt` para el tiempo activo |
| estadísticas | `GET /containers/{id}/stats?stream=false&one-shot=true` | CPU y memoria |

El cliente devuelve el texto JSON; la interpretación la hacen los lectores
(3.3), que son funciones puras.

### 3.3 Lectores de JSON (funciones puras, con tests)

- **Lista** → por contenedor: `Id`, `Name` (primer nombre sin `/` inicial),
  `Image`, `State` (`running`, `exited`, `restarting`, `paused`, `created`,
  `dead`…).
- **Detalle** → `StartedAt` (`DateTimeOffset`).
- **Estadísticas** → contadores de CPU (`cpu_stats.cpu_usage.total_usage`,
  `cpu_stats.system_cpu_usage`, `cpu_stats.online_cpus`) y memoria:
  - usada = `memory_stats.usage` − caché de archivos (`memory_stats.stats.inactive_file`
    en cgroup v2; `memory_stats.stats.total_inactive_file` en cgroup v1;
    si no existe ninguna, `usage` tal cual), como `docker stats`;
  - límite = `memory_stats.limit`.
- JSON malformado o campos ausentes → fallo sin excepción no controlada.

**CPU %** por contenedor, comparando con la lectura anterior del mismo `Id`
(con `one-shot=true` Docker no devuelve lectura previa):

```
cpu% = (Δtotal_usage / Δsystem_cpu_usage) × online_cpus × 100
```

- Primera lectura de un contenedor → `null`.
- `Δsystem_cpu_usage <= 0` o `Δtotal_usage < 0` (contenedor reiniciado) → `null`.
- Resultado limitado a `[0, 100 × online_cpus]` (misma escala que `docker stats`).
- Las lecturas previas de contenedores que ya no aparecen en la lista se descartan.

### 3.4 Muestreo: `ContainerMetricsSampler`

- `BackgroundService` singleton, **separado** de `HostMetricsSampler` para que
  un Docker lento no retrase las métricas de la máquina. Mismo patrón:
  lectura inicial, `PeriodicTimer(RefreshInterval)` (con el mismo fallback a
  2 s si el intervalo es ≤ 0), `Current` con `Volatile`, evento `Updated`
  invocado de forma segura por suscriptor.
- Cada ciclo: pide la lista; para los contenedores `running`, pide detalle y
  estadísticas **en paralelo**.
- Snapshot inmutable:

  ```csharp
  public sealed record ContainersSnapshot(
      IReadOnlyList<ContainerInfo>? Containers,   // null = No disponible
      DateTimeOffset Timestamp);

  public sealed record ContainerInfo(
      string Id, string Name, string Image, string State,
      TimeSpan? Uptime, double? CpuPercent, UsageBytes? Memory);
  ```

- Fallos:
  - `DockerApiUrl` vacío → `Containers = null`, sin peticiones ni logs.
  - Falla la lista (proxy caído, timeout, JSON inválido) → `Containers = null`.
  - Falla detalle o estadísticas de un contenedor → solo sus campos
    `Uptime` / `CpuPercent` / `Memory` a `null`.
  - Log una sola vez por tipo de fallo mientras dure (patrón de la entrega 1).

### 3.5 Interfaz en `/sistema`

- Título `Contenedores` (h2) debajo de las cinco tarjetas y tabla Bootstrap
  dentro de `table-responsive` (desplazamiento horizontal propio en móvil).
- Columnas: Nombre, Imagen, Estado, Tiempo activo, CPU, RAM.
- Estado con badge: verde "En marcha" (`running`), gris "Parado"
  (`exited`, `created`), amarillo "Reiniciando" (`restarting`) / "En pausa"
  (`paused`), rojo con el estado original para cualquier otro (`dead`…).
- Orden: primero `running`, luego el resto; alfabético por nombre dentro de
  cada grupo.
- Tiempo activo con `MetricsFormat.Uptime`; CPU con un decimal y `%`.
- RAM `usada / límite`: MB si < 1 GB, GB a partir de ahí (1 GB = 1024³ bytes),
  cultura actual.
- Valores `null` o contenedor no en marcha → `—`.
- Lista vacía → "No hay contenedores". `Containers == null` → "No disponible".
- La página se suscribe también a `ContainerMetricsSampler.Updated` y se
  desuscribe en `Dispose`.

## 4. Docker

`docker-compose.yml`:

```yaml
services:
  docker-proxy:
    image: tecnativa/docker-socket-proxy:v0.4.1
    environment:
      - CONTAINERS=1
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
    restart: unless-stopped

  dashboard:
    # (configuración existente)
    environment:
      - Sistema__DockerApiUrl=http://docker-proxy:2375
    depends_on:
      - docker-proxy
```

- Comentarios en español para principiante: red interna de Compose, por qué
  el proxy no publica puertos, qué permite `CONTAINERS=1`, `depends_on`.
- `CLAUDE.md`: línea en Stack sobre el proxy y `Sistema__DockerApiUrl`.

## 5. Tests (xUnit)

En `tests/DashBoard.Modules.Sistema.Tests`:

- **Lectores** con fixtures JSON reales de la API de Docker: lista con
  contenedores `running`, `exited` y `restarting`; detalle con `StartedAt`;
  estadísticas cgroup v2 y cgroup v1; JSON malformado / campos ausentes.
- **CPU %**: dos lecturas → valor esperado; primera lectura `null`;
  contadores sin cambio `null`; contador reiniciado `null`; límite superior.
- **Sampler** con un `HttpMessageHandler` falso (sin Docker real):
  - todo correcto → lista con métricas;
  - la lista falla → `Containers == null`;
  - estadísticas de un contenedor fallan → solo ese con `CpuPercent`/`Memory` `null`;
  - `DockerApiUrl` vacío → ninguna petición y `Containers == null`.
- **Formato** MB/GB.

## 6. Criterios de terminado

1. `dotnet build` y `dotnet test` pasan.
2. `dotnet run` en Windows: la sección muestra "No disponible" sin errores.
3. `docker compose up --build`: `/sistema` lista al menos el DashBoard y el
   proxy con CPU y RAM refrescándose.
4. El proxy rechaza escrituras: un `POST` a la API desde la red interna de
   Compose devuelve `403` (la imagen del DashBoard no trae `curl`; se usa un
   contenedor temporal, p. ej. `curlimages/curl`, en la red `dashboard_default`).
