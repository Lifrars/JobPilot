# ADR 0002 — Mediator propio en lugar de MediatR

- **Estado:** aceptada · **Fecha:** 2026-10-26

## Contexto
Desde 2025, **MediatR** y **AutoMapper** pasaron a licencia comercial para empresas, y **FluentAssertions** (v8+)
también. Muchas empresas están migrando fuera de ellas.

## Decisión
- Mediator propio (~60 líneas): `ISender` + wrappers genéricos cacheados (`Sender.cs`).
- Pipeline con decorators de genéricos abiertos registrados con **Scrutor** (licencia MIT).
- Mapeo manual con métodos `ToResponse()`.
- Aserciones con **Shouldly**.

## Consecuencias
- ✅ Sin riesgo de licencias; demuestra comprensión real de genéricos y reflexión.
- ✅ Mapeo explícito: errores en compilación, no en ejecución.
- ⚠️ Mantenemos ese código nosotros. Alternativa sin reflexión: el paquete `Mediator` (source generator, MIT).
