# DashBoard

Contexto específico de este proyecto. Complementa al `CLAUDE.md` general
de `/proyectos` (Docker/ARM64, Git, commits con confirmación), que sigue
aplicando. El proceso de trabajo lo dirigen las skills de Superpowers.

## Propósito

Centro de operaciones personal: ver parámetros, analíticas y datos del
resto de proyectos del usuario y de servicios externos (p. ej. métricas
de entrenamiento sincronizadas desde una app de ejercicio).

## Stack

- ASP.NET Core Blazor Server (.NET 10), proyecto `src/DashBoard.Core`.
- UI con Bootstrap 5.3.3 y Bootstrap Icons servidos en local desde
  `wwwroot/lib` (sin CDN). Tema claro/oscuro (oscuro por defecto)
  persistido en cookie.
- Docker: imagen multi-stage `dotnet/sdk:10.0` → `dotnet/aspnet:10.0`,
  expuesta en el puerto 8080. Las claves de Data Protection se guardan
  en el volumen `dataprotection-keys` (montado en `/keys`).

## Arquitectura: módulos independientes

DashBoard es un proyecto principal (`DashBoard.Core`) al que se le van
agregando **módulos**. Cada vez que se conecta con la API de otro
proyecto o servicio externo, se añade un módulo nuevo.

Reglas estructurales (obligatorias en cualquier diseño o plan):
- Cada módulo es su propio proyecto `.csproj` dentro de la solución
  (`src/Modules/DashBoard.Modules.<Nombre>/`).
- Un módulo solo puede referenciar a `DashBoard.Core`.
- Ningún módulo referencia a otro módulo. El compilador debe impedirlo
  (no añadir la referencia de proyecto entre módulos), no basta con
  disciplina o convención.
- Cada módulo nuevo aparece como tarjeta en el hub de Inicio y como
  entrada en la navegación lateral.

Esto permite añadir o quitar módulos sin que se afecten entre sí.

## Comandos

- Compilar: `dotnet build DashBoard.sln`
- Ejecutar en local: `dotnet run --project src/DashBoard.Core`
- Docker: `docker compose up --build` → http://localhost:8080
  (los comandos de Docker que despliegan los ejecuta el usuario; Claude
  los explica).

## Documentos de Superpowers

Los specs (`superpowers:brainstorming`) y los planes
(`superpowers:writing-plans`) se guardan en las rutas por defecto de las
skills y se versionan con el código:
- `docs/superpowers/specs/`
- `docs/superpowers/plans/`
