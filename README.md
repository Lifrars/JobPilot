# JobPilot 🧭

API para gestionar tu búsqueda de empleo **con IA**: registra postulaciones y entrevistas, analiza ofertas con un LLM,
calcula qué tan bien encaja tu CV y busca semánticamente entre las ofertas guardadas. También expone un **servidor MCP**
para que agentes como Claude consulten tu pipeline.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![C# 14](https://img.shields.io/badge/C%23-14-239120)
![Aspire 13](https://img.shields.io/badge/Aspire-13-6C3BD1) ![PostgreSQL + pgvector](https://img.shields.io/badge/PostgreSQL-pgvector-336791)

## Qué demuestra este proyecto
| Tema | Implementación |
|---|---|
| Arquitectura | Clean Architecture (4 capas) + vertical slices, reglas verificadas con tests de arquitectura |
| Patrones | Mediator/CQRS propio, Decorator, Repository + UoW, Specification, State, Strategy, Composite, Outbox, Result, Null Object, Template Method, Factory |
| Genéricos | `Entity<TId>`, `IRepository<TAggregate,TId>`, IDs tipados con `static abstract`, decorators de genéricos abiertos, smart enums, converters EF genéricos |
| IA | Microsoft.Extensions.AI (Claude / Ollama / OpenAI), structured output, embeddings + pgvector, servidor MCP |
| Calidad | xUnit v3 + Shouldly + NSubstitute, Testcontainers, NetArchTest, `TreatWarningsAsErrors`, CI en GitHub Actions |
| Seguridad | ASP.NET Core Identity (bearer tokens), aislamiento multiusuario con global query filters genéricos de EF Core 10, rate limiting por usuario |
| Operación | .NET Aspire, OpenTelemetry (SQL + LLM), ProblemDetails, rate limiting, HybridCache, Central Package Management |

## Ejecutar
Requisitos: .NET 10 SDK y Docker Desktop.

```bash
dotnet run --project src/JobPilot.AppHost
```
Se abre el dashboard de Aspire. Desde ahí entra a la API: `/scalar` muestra la documentación interactiva.

### Autenticación
Todos los endpoints de `/api/v1` (salvo `/auth`) y el servidor MCP exigen un usuario. Cada usuario ve **solo sus datos**.
1. `POST /api/v1/auth/register` con `{ "email": "...", "password": "..." }` (mínimo 8 caracteres, con mayúscula, número y símbolo).
2. `POST /api/v1/auth/login` con las mismas credenciales: devuelve `accessToken` (1 h) y `refreshToken`.
3. En Scalar, pega el `accessToken` en *Authentication → Bearer*. Desde código, envía `Authorization: Bearer <accessToken>`.
4. Renuévalo con `POST /api/v1/auth/refresh` `{ "refreshToken": "..." }`.

### Activar la IA (opcional)
Sin configuración, la app funciona y los endpoints de IA responden `503`. Hay dos formas de activarla.

**Gratis y local con Ollama:**
```bash
ollama pull llama3.2
ollama pull nomic-embed-text
dotnet user-secrets --project src/JobPilot.Api set "AI:Chat:Provider" "Ollama"
dotnet user-secrets --project src/JobPilot.Api set "AI:Embeddings:Provider" "Ollama"
```

**Con Claude (Anthropic) para el chat y Ollama para los embeddings:**
```bash
dotnet user-secrets --project src/JobPilot.Api set "AI:Chat:Provider" "Anthropic"
dotnet user-secrets --project src/JobPilot.Api set "AI:Chat:ApiKey" "<tu-api-key>"
dotnet user-secrets --project src/JobPilot.Api set "AI:Embeddings:Provider" "Ollama"
```

### Conectar el servidor MCP a Claude Code
```bash
claude mcp add --transport http jobpilot https://localhost:7088/mcp --header "Authorization: Bearer <accessToken>"
```
El token dura 1 hora; cuando expire, haz login de nuevo y actualiza el header.
Después pregúntale a Claude: *"¿cuál es mi tasa de respuesta?"* o *"mueve la postulación X a Interviewing"*.

## Tests
```bash
dotnet test                                                   # todo (integración requiere Docker)
dotnet test --project tests/JobPilot.Domain.UnitTests         # solo dominio
```

## Endpoints principales (`/api/v1`)
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/auth/register` · `/auth/login` · `/auth/refresh` | 🔓 Cuenta y tokens (públicos) |
| POST | `/companies` | Crear empresa |
| POST | `/applications` | Crear postulación (estado inicial `Wishlist`) |
| GET | `/applications?status=&companyId=&search=&page=` | Listar con filtros |
| POST | `/applications/{id}/status` | Cambiar estado (valida transiciones) |
| POST | `/applications/{id}/interviews` | Agendar entrevista |
| GET | `/applications/stats` | Métricas del pipeline |
| POST | `/applications/{id}/analyze` | 🤖 Analizar la oferta con LLM |
| GET | `/applications/{id}/match?resumeId=&strategy=` | 🤖 Match CV↔oferta |
| GET | `/applications/search?q=` | 🤖 Búsqueda semántica |
| POST | `/applications/{id}/cover-letter` | 🤖 Borrador de carta de presentación |
| POST | `/resumes` | Guardar una versión del CV |

## Documentación
- [Arquitectura y flujo de una request](docs/arquitectura.md)
- [Patrones de diseño](docs/patrones.md) · [Genéricos](docs/genericos.md)
- [Decisiones de arquitectura (ADRs)](docs/adr/)
- [Guía para entrevistas](docs/entrevista.md)

## Trabajar con Claude Code
`CLAUDE.md` (contexto), `.claude/rules/` (estándares por capa) y `.claude/skills/` (`add-feature`, `add-entity`,
`explain-pattern`, `interview-prep`) hacen que Claude siga las convenciones del proyecto sin tener que repetirlas.
