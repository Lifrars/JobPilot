---
paths:
  - "tests/**/*.cs"
---

# Estándares de testing

- xUnit v3 sobre Microsoft.Testing.Platform. Filtrar con `dotnet test --project <proyecto> --filter-class <Clase>` (no `--filter` de VSTest).
- Nombres: `Metodo_Escenario_ResultadoEsperado`. Estructura Arrange / Act / Assert separada por líneas en blanco.
- Aserciones con Shouldly (`result.ShouldBe(...)`); dobles con NSubstitute. No usar FluentAssertions ni Moq.
- Pasar `TestContext.Current.CancellationToken` a todo método asíncrono.
- Tiempo: `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing), nunca `DateTime.Now`.
- Qué test escribir:
  - Regla de negocio → `JobPilot.Domain.UnitTests` (sin mocks).
  - Handler / pipeline / estrategia → `JobPilot.Application.UnitTests`.
  - Nueva specification o consulta EF → agregar caso en `QueryTranslationTests` (verifica el SQL sin Docker).
  - Flujo HTTP completo → `JobApplicationApiTests` (Testcontainers + pgvector, colección `ApiCollection`). Usar `factory.CreateAuthenticatedClientAsync()`: registra un usuario nuevo por test.
  - Todo endpoint nuevo que lea datos → caso en `UserIsolationTests` (otro usuario recibe 404 / lista vacía).
  - Smoke tests sin BD → `StartupSmokeTests` con `TestAuthHandler` (header `X-Test-User`).
  - Nueva regla de capas → `JobPilot.ArchitectureTests`.
- Los efectos del Outbox son asíncronos: esperar con polling (`WaitUntilAsync`), nunca `Task.Delay` fijo.
