# Módulo Salud — datos de SaludApi en el DashBoard

- **Fecha:** 2026-10-06
- **Estado:** diseño aprobado en conversación, pendiente de revisión del spec escrito
- **Ramas:** `feature/modulo-salud` (DashBoard) y `feature/resumen-diario` (SaludApi)
- **Proyectos afectados:** DashBoard (módulo nuevo) y SaludApi (endpoint nuevo)

## 1. Objetivo

Ver en `/salud` los datos de salud que la app Android sincroniza en SaludApi:
un resumen de hoy y de la semana arriba, y la evolución en el tiempo debajo.

```
Móvil (app) ──HTTP──▶ SaludApi (Docker) ──▶ Postgres (Docker)
                          ▲
                          │ red Docker "homelab" (nueva)
                    DashBoard (Docker) ──▶ navegador
```

DashBoard **no** accede a Postgres: solo habla con la API de SaludApi, que es
el contrato entre los dos proyectos.

### Requisitos del usuario

- Propósito: **ambas cosas** — vistazo del día a día y progreso a lo largo
  del tiempo.
- Datos: **peso**, **entrenamientos** y **pasos y distancia**.
- Evolución con **selector de periodo**: 30 días / 90 días / 1 año
  (30 días por defecto).
- **Objetivos** configurables (pasos al día, entrenos por semana, peso
  objetivo) para ver el progreso hacia ellos.

### Supuestos aceptados

- Un "día" es un día natural en hora de España (`Europe/Madrid`); una semana
  va de lunes a domingo.
- El peso de un día es la **última** medición de ese día.
- Los datos se piden al abrir la página y al cambiar de periodo; no hay
  refresco en vivo (el móvil sincroniza cada cierto tiempo, no cada segundo).
- Si SaludApi está apagada o mal configurada, el módulo muestra un aviso y el
  resto del DashBoard funciona igual.

### Fuera de alcance

- Calorías (los datos llegan, pero no se muestran; añadirlas es una fila más
  en la tabla de agregación de la sección 2.2).
- Editar o borrar datos desde el DashBoard (solo lectura).
- Gráficas interactivas (zoom, animaciones) y librerías de gráficas en JS.
- Cambiar el endpoint existente de consulta de registros sueltos.

## 2. SaludApi: endpoint de resumen diario

### 2.1 Contrato

`GET /api/health-records/daily?type={tipo}&from={yyyy-MM-dd}&to={yyyy-MM-dd}`

- Política de autorización: la de **consulta** (`ApiKeyScopes.QueryPolicy`,
  cabecera `X-Api-Key` con `SALUDAPI_QUERY_API_KEY`), la misma que el
  endpoint de consulta actual; la clave de ingesta se rechaza.
- `from` y `to` son **fechas** (`DateOnly`), ambas incluidas, interpretadas en
  la zona horaria configurada.
- Respuesta `200`: lista ordenada por fecha; los días sin registros **no**
  aparecen.

```json
[
  { "date": "2026-09-06", "value": 8432, "count": 37 },
  { "date": "2026-09-07", "value": 10211, "count": 41 }
]
```

Errores (`400` con mensaje en español):
- `type` ausente o no admitido → indica los tipos admitidos.
- `from` o `to` ausentes, o `from` posterior a `to`.
- Rango de más de 366 días (límite de seguridad; el selector pide como
  máximo un año).

### 2.2 Agregación por tipo

| Tipo | Campo del `PayloadJson` | `value` | `count` |
|---|---|---|---|
| `Steps` | `count` | suma del día | nº de registros |
| `Distance` | `meters` | suma del día (metros) | nº de registros |
| `Weight` | `kg` | último valor del día (por `StartTime`) | nº de mediciones |
| `ExerciseSession` | — (`EndTime - StartTime`) | minutos totales del día | nº de sesiones |

- Cada registro se asigna al día local de su `StartTime`.
- Un registro con `PayloadJson` sin el campo esperado (o una sesión sin
  `EndTime`) se ignora para `value` y no cuenta en `count`; no rompe la
  respuesta.

### 2.3 Implementación

- `DailyAggregator` (clase pura, sin EF ni HTTP): recibe la lista de
  registros, el tipo y la `TimeZoneInfo`, y devuelve los `DailyValueDto`.
  Concentra las reglas de la tabla anterior.
- El controlador convierte `from`/`to` locales a un rango UTC
  (`[from 00:00 local, to+1 00:00 local)`), lee de Postgres los registros de
  ese tipo y rango, y delega en `DailyAggregator`.
- Configuración: `Health:TimeZone` (por defecto `Europe/Madrid`), variable de
  entorno `Health__TimeZone`. Verificado: la imagen
  `mcr.microsoft.com/dotnet/aspnet:10.0` incluye `/usr/share/zoneinfo/Europe/Madrid`
  en `linux/amd64` y `linux/arm64`.

### 2.4 Tests de SaludApi

- `DailyAggregator`: suma por día, último peso del día, minutos de sesión,
  frontera de medianoche en hora local (un registro a las 00:30 de Madrid
  cuenta para ese día, aunque en UTC sea el día anterior), cambio de horario
  de verano/invierno, payload sin el campo esperado.
- Controlador, con el patrón de `HealthRecordsQueryTests` (acción llamada
  directamente con `UseInMemoryDatabase`): validaciones (`type` no admitido,
  `from > to`, rango mayor de 366 días) y conversión del rango local a UTC
  (registros justo fuera de los límites no se incluyen).
- Autorización: la autenticación por clave ya está probada en
  `ApiKeyAuthenticationHandlerTests`; un test comprueba por reflexión que la
  acción nueva lleva `[Authorize(Policy = ApiKeyScopes.QueryPolicy)]`.

## 3. Conexión entre contenedores: red `homelab`

Cada proyecto de Compose tiene su red privada; para que DashBoard llegue a
SaludApi se crea una red **externa** compartida, una sola vez por máquina:

```
docker network create homelab
```

| Proyecto | Servicio | En `homelab` | Motivo |
|---|---|---|---|
| SaludApi | `saludapi` | sí | DashBoard la consulta |
| SaludApi | `db` | **no** | Postgres sigue privado |
| DashBoard | `dashboard` | sí | hace las consultas |
| DashBoard | `docker-proxy` | **no** | el proxy sigue privado |

- Cada `docker-compose.yml` declara `homelab` como `external: true` y conecta
  solo el servicio indicado (además de su red `default`).
- Desde DashBoard la API se alcanza en `http://saludapi:8080` (nombre del
  servicio y puerto interno). El puerto `8081` publicado se mantiene para el
  móvil.
- Si la red no existe, `docker compose up` falla con
  `network homelab declared as external, but could not be found`. Se
  documenta en el `README`/`CLAUDE.md` de ambos proyectos.

## 4. DashBoard: módulo `DashBoard.Modules.Salud`

Sigue las reglas de módulos del `CLAUDE.md` (proyecto propio en
`src/Modules/DashBoard.Modules.Salud/`, solo referencia a `DashBoard.Core`,
tarjeta en Inicio, entrada en la navegación, `COPY` del `.csproj` en el
`Dockerfile`, proyecto de tests propio). Sin dependencias NuGet nuevas.

- `Title`: "Salud"; `Route`: `salud`; `Icon`: `bi-heart-pulse-fill`.
- `Description`: "Pasos, peso y entrenamientos sincronizados desde el móvil."

### 4.1 Configuración: `SaludOptions` (sección `Salud`)

| Clave | Variable de entorno | Por defecto | Uso |
|---|---|---|---|
| `ApiUrl` | `Salud__ApiUrl` | vacía | URL de SaludApi; vacía desactiva el módulo |
| `ApiKey` | `Salud__ApiKey` | vacía | clave de **consulta** de SaludApi |
| `TimeZone` | `Salud__TimeZone` | `Europe/Madrid` | qué es "hoy" y "esta semana" |
| `StepsPerDay` | `Salud__StepsPerDay` | sin objetivo | objetivo de pasos al día |
| `WorkoutsPerWeek` | `Salud__WorkoutsPerWeek` | sin objetivo | objetivo de entrenos por semana |
| `TargetWeightKg` | `Salud__TargetWeightKg` | sin objetivo | peso objetivo |

- `ApiKey` es un secreto: DashBoard pasa a tener `.env` (no versionado: se
  añade `.env` a `.gitignore`, que hoy no lo ignora) y `.env.example`
  (versionado). El
  `docker-compose.yml` pasa `Salud__ApiKey=${SALUD_API_KEY}`.
- Cada objetivo es opcional: si falta, no se dibuja ni su barra de progreso
  ni su línea en la gráfica.

### 4.2 Cliente: `SaludApiClient`

- `HttpClient` propio con timeout corto (5 s), al estilo de `DockerApiClient`;
  añade la cabecera `X-Api-Key`.
- `GetDailyAsync(type, from, to)` → `GET /api/health-records/daily`.
- `GetWorkoutsAsync(fromUtc)` → `GET /api/health-records?type=ExerciseSession&from=…`
  (endpoint existente) para la lista de últimos entrenamientos con su título
  (`PayloadJson.title`) y duración.
- Devuelve un resultado que distingue: datos, no configurado, SaludApi
  inaccesible (error de red o timeout), clave rechazada (401/403) y respuesta
  inesperada. No lanza excepciones hacia la página.

### 4.3 Cálculos: `SaludSummary` (funciones puras, con tests)

- **Hoy:** pasos y km de hoy; porcentaje respecto a `StepsPerDay`.
- **Semana actual (lunes–domingo):** nº de entrenos frente a
  `WorkoutsPerWeek`.
- **Peso:** último peso registrado, variación respecto al último peso de
  hace ~30 días (el más cercano anterior) y distancia al `TargetWeightKg`.
- **Series del periodo:**
  - Pasos: un punto por día con 30 y 90 días; con 1 año, **media diaria por
    semana**. Los días sin datos cuentan como 0.
  - Peso: un punto por día con medición (sin rellenar huecos).
  - Entrenos: nº de sesiones por semana.
- **Últimos entrenamientos:** los 5 más recientes (fecha, título, minutos).

### 4.4 Gráficas: SVG en Razor

Componentes `BarChart` y `LineChart` del módulo, sin JavaScript ni CDN:
- Funcionan igual en ARM64 y respetan el tema claro/oscuro (colores desde
  variables CSS de Bootstrap).
- Línea discontinua para el objetivo cuando existe.
- Detalle al pasar el ratón con `<title>` de SVG (fecha y valor; en pasos
  también la distancia).
- La geometría (escalas, posiciones, ancho de barras) se calcula en una clase
  pura (`ChartGeometry`) con tests; los componentes solo la pintan.

### 4.5 Página `/salud`

```
HOY Y ESTA SEMANA
[Pasos hoy + progreso] [Km hoy] [Entrenos semana x/obj] [Peso + variación 30 d + obj]

EVOLUCIÓN                    [30 días] [90 días] [1 año]
Pasos por día (o media semanal en 1 año)   — línea de objetivo
Peso                                       — línea de objetivo
Entrenos por semana                        — línea de objetivo

ÚLTIMOS ENTRENAMIENTOS
fecha · título · minutos (5 más recientes)
```

- Pide los datos al abrir la página y al cambiar de periodo, mostrando un
  indicador de carga.
- Mensajes de estado (en lugar del contenido):
  - No configurado: "El módulo Salud no está configurado (falta `Salud__ApiUrl`)."
  - Inaccesible: "No se puede conectar con SaludApi."
  - Clave rechazada: "SaludApi ha rechazado la clave (revisa `Salud__ApiKey`)."
  - Sin datos en el periodo: se muestran las secciones vacías con "Sin datos".
- Formato numérico en español (`es-ES`), como el módulo Sistema.

## 5. Docker

- **SaludApi** (`docker-compose.yml`): `saludapi` se une a `homelab`
  (externa). Sin otros cambios.
- **DashBoard** (`docker-compose.yml`): `dashboard` se une a `homelab`
  (externa) y recibe `Salud__ApiUrl=http://saludapi:8080`,
  `Salud__ApiKey=${SALUD_API_KEY}` y los objetivos (comentados en
  `.env.example`).
- **Dockerfile** de DashBoard: `COPY` del nuevo `.csproj` antes del restore.
- No se añaden imágenes nuevas.

## 6. Tests (xUnit)

- SaludApi: ver 2.4.
- `tests/DashBoard.Modules.Salud.Tests`:
  - `SaludApiClient` con un `HttpMessageHandler` falso (patrón de
    `FakeDockerHandler`): URL y cabecera enviadas, parseo de la respuesta,
    401/403, error de red y timeout, módulo sin configurar.
  - `SaludSummary`: progreso de objetivos, semana lunes–domingo, media semanal
    de pasos, variación de peso a 30 días, objetivos ausentes, días sin datos.
  - `ChartGeometry`: escalas con valores 0, un solo punto, objetivo por encima
    del máximo.
- `DashBoard.Architecture.Tests` cubre el nuevo módulo sin cambios.

## 7. Criterios de terminado

- `dotnet test` en verde en los dos proyectos.
- `docker network create homelab` + `docker compose up --build` en SaludApi y
  en DashBoard: `/salud` muestra los datos reales sincronizados desde el
  móvil (pasos, km, peso y entrenamientos) y el selector de periodo funciona.
- Con SaludApi parada, `/salud` muestra "No se puede conectar con SaludApi" y
  el resto del DashBoard funciona.
- `CLAUDE.md` y documentación de ambos proyectos actualizados (red `homelab`,
  endpoint nuevo, configuración `Salud`).
