# ADR 0004 — IA con Microsoft.Extensions.AI y servidor MCP

- **Estado:** aceptada · **Fecha:** 2026-10-26

## Contexto
Queremos analizar ofertas con LLM, generar embeddings y no quedar atados a un proveedor.

## Decisión
- Abstracciones **Microsoft.Extensions.AI** (`IChatClient`, `IEmbeddingGenerator`), que cumplen para la IA el rol que `ILogger` cumple para el logging.
- Proveedores intercambiables por configuración: **Anthropic** (SDK oficial, `AsIChatClient`), **Ollama** (local y gratis) y **OpenAI**.
- **Structured output** con `GetResponseAsync<T>()` para respuestas tipadas.
- Pipeline de decorators: `UseOpenTelemetry()` + `UseLogging()` (se ven los tokens y la latencia en Aspire).
- Application define puertos propios (`IJobPostingAnalyzer`...), así que no depende ni siquiera de M.E.AI.
- **Servidor MCP** (`ModelContextProtocol.AspNetCore`) en `/mcp` que reutiliza `ISender`.
- **Null Object** cuando no hay IA configurada.

## Consecuencias
- ✅ Cambiar de Claude a Ollama es cambiar una línea de configuración.
- ✅ Los tests usan un `FakeEmbeddingService` determinista.
- ⚠️ Anthropic no ofrece embeddings: hay que combinarlo con Ollama u OpenAI.
- ⚠️ Costos: los endpoints de IA tienen rate limiting (10/min).
