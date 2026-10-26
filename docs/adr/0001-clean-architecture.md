# ADR 0001 — Clean Architecture en 4 proyectos

- **Estado:** aceptada · **Fecha:** 2026-10-26

## Contexto
JobPilot es un proyecto de portafolio que debe mostrar separación de responsabilidades, testabilidad y una
arquitectura reconocible en entrevistas para roles backend .NET.

## Decisión
Cuatro proyectos (Domain, Application, Infrastructure, Api) con la regla de dependencias hacia adentro,
verificada por `JobPilot.ArchitectureTests`. Dentro de Application, organización por feature (vertical slice).
La inyección de dependencias se compone en `Program.cs` mediante métodos `AddXxx()` por capa, sin proyecto IoC separado.

## Alternativas consideradas
- **Vertical Slice Architecture pura** (un solo proyecto): menos ceremonia, pero se ve menos la inversión de dependencias.
- **N-capas clásico** (UI/BLL/DAL/IoC): acopla la lógica a la persistencia y es menos valorado hoy.

## Consecuencias
- ✅ El dominio se prueba sin base de datos; la infraestructura se puede cambiar.
- ✅ Las reglas de capas son verificables en CI.
- ⚠️ Hay más archivos y mapeos (DTOs). Se acepta por el valor educativo y de mantenimiento.
