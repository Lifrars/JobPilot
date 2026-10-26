---
paths:
  - "src/JobPilot.Infrastructure/**/*.cs"
---

# Estándares de la capa Infrastructure

- Configuración EF: una clase `internal sealed class XConfiguration : EntityConfiguration<X, XId>` por entidad, que implementa solo `ConfigureEntity` (la base ya define clave, `ValueGeneratedNever` e ignora `DomainEvents`).
- No configurar converters de IDs a mano: `AddStronglyTypedIdConversions` los descubre por reflexión. Los smart enums nuevos se registran en `JobPilotDbContext.ConfigureConventions`.
- Conversores con `HasConversion<TConverter>()` / `HasConversion<TConverter, TComparer>()` (evita problemas de nulabilidad).
- Tras cambiar el modelo: generar una migración (ver comando en CLAUDE.md) y revisar el SQL generado. No editar migraciones ya aplicadas.
- Repositorios: usar el genérico `IRepository<,>` → `EfRepository<,>`; crear uno específico solo si hay consultas propias del aggregate (ej. `JobApplicationRepository`).
- SQL crudo: solo con `SqlQuery`/`FromSql` interpolado (parámetros seguros). Nunca concatenar strings. **No aplica query filters**: agregar `WHERE "OwnerId" = {ownerId.Value}` siempre.
- Aislamiento: el filtro `JobPilotDbContext.OwnerFilter` se registra solo para todo `IOwnedByUser`. Para saltarlo usar `IgnoreQueryFilters([JobPilotDbContext.OwnerFilter])` y justificarlo en un comentario.
- Procesos en segundo plano (sin HttpContext): `CurrentUser.RunAsSystem()` al crear el scope, nunca en código de request.
- Identity: `AppUser` y las tablas `AspNet*` viven en el mismo `JobPilotDbContext` (hereda de `IdentityDbContext`).
- IA: el resto del sistema solo ve `IChatClient` / `IEmbeddingGenerator`. Un proveedor nuevo = un caso más en `AiServiceCollectionExtensions`. Atrapar `HttpRequestException` y devolver `AiErrors.ProviderUnavailable`.
- Secretos (API keys) nunca en appsettings: `dotnet user-secrets` en `src/JobPilot.Api`.
