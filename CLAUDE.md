# DashBoard

Contexto específico de este proyecto. Complementa al `CLAUDE.md` general
de `/proyectos` (Docker/ARM64, Git, commits con confirmación), que sigue
aplicando. El proceso de trabajo lo dirigen las skills de Superpowers.

## Propósito

Centro de operaciones personal: ver parámetros, analíticas y datos del
resto de proyectos del usuario y de servicios externos (p. ej. métricas
de entrenamiento sincronizadas desde una app de ejercicio).

## Stack

- ASP.NET Core Blazor Server (.NET 10), aplicación en `src/DashBoard.Web`.
- UI con Bootstrap 5.3.3 y Bootstrap Icons servidos en local desde
  `wwwroot/lib` (sin CDN). Tema claro/oscuro (oscuro por defecto)
  persistido en cookie.
- Docker: imagen multi-stage `dotnet/sdk:10.0` → `dotnet/aspnet:10.0`,
  expuesta en el puerto 8080. Las claves de Data Protection se guardan
  en el volumen `dataprotection-keys` (montado en `/keys`).
- El módulo Sistema lee métricas de la máquina desde `/proc`, `/sys` y `/`,
  montados en solo lectura en el contenedor (`/host/proc`, `/host/sys`,
  `/host/root`) y configurados en la sección `Sistema` (`Sistema__ProcPath`,
  `Sistema__SysPath`, `Sistema__DiskPath`). El compose fija `LANG=es_ES.UTF-8`
  para el formato numérico en español.

## Arquitectura: módulos independientes

DashBoard es una aplicación (`DashBoard.Web`) a la que se le van agregando
**módulos**. `DashBoard.Web` registra los módulos (lista en `Program.cs`) y
genera con ellos Inicio y la navegación; `DashBoard.Core` es una biblioteca
sin dependencias que define el contrato `IDashboardModule`. Cada vez que se
conecta con la API de otro proyecto o servicio externo, se añade un módulo
nuevo.

Reglas estructurales (obligatorias en cualquier diseño o plan):
- Cada módulo es su propio proyecto `.csproj` dentro de la solución
  (`src/Modules/DashBoard.Modules.<Nombre>/`).
- Un módulo solo puede referenciar a `DashBoard.Core`.
- Ningún módulo referencia a otro módulo. El compilador debe impedirlo
  (no añadir la referencia de proyecto entre módulos), no basta con
  disciplina o convención.
- Cada módulo nuevo aparece como tarjeta en el hub de Inicio y como
  entrada en la navegación lateral.

Esto permite añadir o quitar módulos sin que se afecten entre sí. El test
`DashBoard.Architecture.Tests` hace cumplir estas reglas escaneando los
`.csproj` de `src/Modules/`.

### Añadir un módulo

1. Crear `src/Modules/DashBoard.Modules.<Nombre>` como Razor class library
   y añadirlo a la solución.
2. Referenciar solo `DashBoard.Core` desde el módulo (implementa
   `IDashboardModule`).
3. Referenciar el módulo desde `DashBoard.Web`.
4. Añadir el módulo a la lista de `Program.cs`.
5. Añadir su `COPY` del `.csproj` en el `Dockerfile` (antes del restore).
6. Crear el proyecto de tests `tests/DashBoard.Modules.<Nombre>.Tests`.

## Comandos

- Compilar: `dotnet build DashBoard.sln`
- Tests: `dotnet test DashBoard.sln`
- Ejecutar en local: `dotnet run --project src/DashBoard.Web`
- Docker: `docker compose up --build` → http://localhost:8080
  (los comandos de Docker que despliegan los ejecuta el usuario; Claude
  los explica).

## Documentos de Superpowers

Los specs (`superpowers:brainstorming`) y los planes
(`superpowers:writing-plans`) se guardan en las rutas por defecto de las
skills y se versionan con el código:
- `docs/superpowers/specs/`
- `docs/superpowers/plans/`
