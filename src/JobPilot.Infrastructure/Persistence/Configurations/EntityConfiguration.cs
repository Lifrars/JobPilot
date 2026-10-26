using JobPilot.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPilot.Infrastructure.Persistence.Configurations;

/// <summary>
/// Template Method: la base fija los pasos comunes (clave, IDs generados por el cliente, ignorar eventos)
/// y cada subclase solo implementa <see cref="ConfigureEntity"/>.
/// </summary>
internal abstract class EntityConfiguration<TEntity, TId> : IEntityTypeConfiguration<TEntity>
    where TEntity : Entity<TId>
    where TId : struct, IEquatable<TId>
{
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(entity => entity.Id);

        // Los IDs los genera el dominio (Guid v7). Sin esto, EF trataría a una entidad hija nueva
        // con ID asignado como "existente" y haría UPDATE en vez de INSERT.
        builder.Property(entity => entity.Id).ValueGeneratedNever();

        if (typeof(IAggregateRoot).IsAssignableFrom(typeof(TEntity)))
        {
            builder.Ignore(nameof(IAggregateRoot.DomainEvents));
        }

        ConfigureEntity(builder);
    }

    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
}
