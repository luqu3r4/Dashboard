# DashBoard

Contexto específico de este proyecto. Complementa al `CLAUDE.md` general
de `/proyectos` (stack por defecto, Docker/ARM64, Git, planificación por
funcionalidad/tarea/subtarea, carpeta `task/`, etc.), que sigue aplicando.

## Propósito

Centro de operaciones personal: ver parámetros, analíticas y datos del
resto de proyectos del usuario y otros aspectos (p. ej. métricas de
entrenamiento sincronizadas desde una app de ejercicio).

## Stack

ASP.NET Core Blazor Server (.NET 10).

## Arquitectura: módulos independientes

DashBoard es un proyecto principal (`DashBoard.Core`) al que se le van
agregando **módulos**. Cada vez que se conecta con la API de otro
proyecto o servicio externo, se añade un módulo nuevo.

Regla estructural:
- Cada módulo es su propio proyecto `.csproj` dentro de la solución
  (`src/Modules/DashBoard.Modules.<Nombre>/`).
- Un módulo solo puede referenciar a `DashBoard.Core`.
- Ningún módulo referencia a otro módulo. El compilador debe impedirlo
  (no añadir la referencia de proyecto entre módulos), no basta con
  disciplina o convención.

Esto permite añadir o quitar módulos sin que se afecten entre sí.
