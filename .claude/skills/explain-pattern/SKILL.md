---
name: explain-pattern
description: Explica en español un patrón de diseño, principio o técnica de genéricos usando el código REAL de JobPilot (archivo y línea), con trade-offs y preguntas de entrevista. Usar cuando el usuario pregunte "¿cómo funciona X?", "¿por qué usamos Y?" o "explícame el patrón Z".
---

# Explicar un patrón con el código del repo

1. **Ubica el código real.** Usa `docs/patrones.md` como índice y lee el archivo antes de explicar.
   No inventes ejemplos genéricos si el patrón existe en el repo.
2. **Estructura la respuesta así** (en español, tono de mentor, sin relleno):
   - **Problema**: qué dolor resuelve, en 1–2 frases y con un ejemplo de este dominio.
   - **Cómo está implementado aquí**: enlaces `ruta/archivo.cs:línea` y un fragmento corto.
   - **Recorrido**: qué pasa paso a paso en una request real (ej. `POST /api/v1/applications/{id}/status`).
   - **Trade-offs**: cuándo NO usarlo y qué alternativa existe.
   - **Cómo lo contarías en una entrevista**: 3–4 frases en primera persona.
   - **2 preguntas que te podrían hacer** y una respuesta breve a cada una.
3. Si el usuario quiere practicar, propone un mini-ejercicio concreto en el repo
   (ej. "agrega una estrategia de match `SeniorityMatchStrategy`") y revisa su solución.

Patrones disponibles: Mediator/CQRS, Decorator, Repository + Unit of Work, Specification, State, Strategy,
Composite, Factory Method, Template Method, Null Object, Outbox, Result, Options, Smart Enum, Strongly-typed IDs.
