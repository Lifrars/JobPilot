---
name: add-entity
description: Agrega un nuevo aggregate root a JobPilot (ID fuertemente tipado, factory, errores, configuración EF, migración y repositorio). Usar cuando se pida modelar una nueva entidad o tabla, por ejemplo "Contact", "Offer" o "Reminder".
---

# Agregar un aggregate

Referencia real: `src/JobPilot.Domain/Resumes/Resume.cs` (aggregate simple) y
`src/JobPilot.Domain/JobApplications/JobApplication.cs` (aggregate con colecciones y estado).

## 1. Domain — `src/JobPilot.Domain/<Plural>/<Name>.cs`
```csharp
public readonly record struct ContactId(Guid Value) : IStronglyTypedId<ContactId, Guid>
{
    public static ContactId New() => new(Guid.CreateVersion7());
    public static ContactId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public sealed class Contact : AggregateRoot<ContactId>
{
    private Contact(ContactId id, string name, DateTimeOffset createdAt) : base(id) { Name = name; CreatedAt = createdAt; }
    private Contact() { }

    public string Name { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Contact> Create(string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name)) return ContactErrors.NameRequired;
        return new Contact(ContactId.New(), name.Trim(), now);
    }
}

public static class ContactErrors
{
    public static readonly Error NameRequired = Error.Validation("Contact.NameRequired", "The name is required.");
    public static Error NotFound(ContactId id) => Error.NotFound("Contact.NotFound", $"The contact '{id}' was not found.");
}
```
El converter del ID se registra solo (`AddStronglyTypedIdConversions`).

## 2. Infrastructure
- `DbSet<Contact> Contacts => Set<Contact>();` en `JobPilotDbContext`.
- Configuración en `Persistence/Configurations/EntityConfigurations.cs`:
```csharp
internal sealed class ContactConfiguration : EntityConfiguration<Contact, ContactId>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Contact> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
    }
}
```
- Repositorio: inyecta `IRepository<Contact, ContactId>` (genérico abierto, ya registrado). Crea `IContactRepository` solo si necesitas consultas propias.

## 3. Migración
```bash
dotnet ef migrations add AddContacts --project src/JobPilot.Infrastructure --startup-project src/JobPilot.Infrastructure --output-dir Persistence/Migrations
```

## 4. Tests
- `tests/JobPilot.Domain.UnitTests/<Name>Tests.cs`: factory válida e inválida, cada regla de negocio.
- `LayerTests.Aggregates_DoNotExposePublicConstructors` verificará el diseño automáticamente.

Después usa la skill `add-feature` para exponer los casos de uso.
