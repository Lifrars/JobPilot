using System.Reflection;
using System.Text.Json;
using JobPilot.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

namespace JobPilot.Infrastructure.Persistence;

/// <summary>
/// Un único converter genérico para TODOS los IDs fuertemente tipados.
/// Nota: un árbol de expresión no puede llamar a un miembro <c>static abstract</c> directamente (CS8927),
/// por eso pasamos por el método auxiliar <see cref="Create"/>.
/// </summary>
public sealed class StronglyTypedIdConverter<TId, TValue>() : ValueConverter<TId, TValue>(id => id.Value, value => Create(value))
    where TId : struct, IStronglyTypedId<TId, TValue>
    where TValue : notnull
{
    public static TId Create(TValue value) => TId.From(value);
}

/// <summary>Guarda un smart enum por su nombre (legible en la base de datos).</summary>
public sealed class EnumerationConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => value.Name,
    name => Enumeration<TEnum>.FromName(name)!)
    where TEnum : Enumeration<TEnum>;

/// <summary>Serializa cualquier objeto a una columna jsonb.</summary>
public sealed class JsonValueConverter<T>() : ValueConverter<T, string>(
    value => JsonSerializer.Serialize(value, JsonSerializerOptions.Default),
    json => JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Default)!);

/// <summary>Mapea el <c>float[]</c> del dominio (que no conoce pgvector) al tipo <see cref="Vector"/> de PostgreSQL.</summary>
public sealed class FloatArrayToVectorConverter() : ValueConverter<float[], Vector>(
    array => new Vector(array),
    vector => vector.ToArray());

public sealed class FloatArrayComparer() : ValueComparer<float[]>(
    (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
    array => array.Aggregate(0, (hash, value) => HashCode.Combine(hash, value)),
    array => array.ToArray());

internal static class ConventionExtensions
{
    /// <summary>Busca por reflexión todos los IStronglyTypedId del dominio y les asigna su converter genérico.</summary>
    public static ModelConfigurationBuilder AddStronglyTypedIdConversions(this ModelConfigurationBuilder builder, Assembly assembly)
    {
        var idTypes = assembly.GetTypes()
            .Where(type => type.IsValueType && !type.IsAbstract)
            .Select(type => (Type: type, Interface: type.GetInterfaces().FirstOrDefault(IsStronglyTypedIdInterface)))
            .Where(pair => pair.Interface is not null);

        foreach (var (idType, idInterface) in idTypes)
        {
            var valueType = idInterface!.GetGenericArguments()[1];
            var converterType = typeof(StronglyTypedIdConverter<,>).MakeGenericType(idType, valueType);
            builder.Properties(idType).HaveConversion(converterType);
        }

        return builder;
    }

    private static bool IsStronglyTypedIdInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IStronglyTypedId<,>);
}
