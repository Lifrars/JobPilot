---
paths:
  - "src/JobPilot.Domain/**/*.cs"
---

# Estándares de la capa Domain

- Cero dependencias: no agregar `PackageReference` ni `using` de EF Core, ASP.NET o Application.
- Un aggregate = una carpeta (`Companies/`, `JobApplications/`, `Resumes/`) con: aggregate, ID tipado, errores, eventos y, si aplica, repositorio específico.
- Aggregate:
  - `public sealed class X : AggregateRoot<XId>, IOwnedByUser` con `public UserId OwnerId { get; private set; }` (aislamiento por usuario).
  - `Create(UserId ownerId, ...)`: el dueño siempre es el primer parámetro.
  - Constructor privado con parámetros + constructor privado vacío para EF.
  - `public static Result<X> Create(...)` valida invariantes y llama a `Raise(new XCreatedDomainEvent(...))`.
  - Propiedades con `private set`; colecciones como `private readonly List<T> _items` + `IReadOnlyCollection<T> Items => _items.AsReadOnly()`.
  - Los métodos de comportamiento devuelven `Result` / `Result<T>` y reciben `DateTimeOffset now` (sin relojes en el dominio).
- Entidades internas (`Interview`) heredan de `Entity<TId>` y tienen constructor `internal`: solo su raíz las crea.
- Value objects: `sealed record` con factory `Create` que devuelve `Result<T>`.
- Estados con comportamiento → subclases de `Enumeration<T>` (patrón State), no `enum` + `switch`.
- Eventos: `sealed record XxxDomainEvent(..., DateTimeOffset OccurredOn) : IDomainEvent`, nombrados en pasado. Usar tipos serializables (IDs tipados, strings), porque viajan como JSON por el Outbox. Incluir `UserId OwnerId` si el handler necesita saber a qué usuario afecta (ej. invalidar su caché).
- Errores: `public static class XErrors` con `Error.Validation/NotFound/Conflict(...)`. Código con formato `Aggregate.Motivo`.
