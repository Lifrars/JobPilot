using System.Reflection;

namespace JobPilot.Domain.Abstractions;

/// <summary>
/// "Smart enum" genérico: como un enum, pero cada valor es un objeto que puede tener comportamiento.
/// Las instancias son los campos <c>public static readonly</c> declarados en <typeparamref name="TEnum"/>.
/// </summary>
public abstract class Enumeration<TEnum> : IEquatable<Enumeration<TEnum>>
    where TEnum : Enumeration<TEnum>
{
    private static readonly Lazy<Dictionary<int, TEnum>> All = new(() =>
        typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(field => typeof(TEnum).IsAssignableFrom(field.FieldType))
            .Select(field => (TEnum)field.GetValue(null)!)
            .ToDictionary(value => value.Id));

    protected Enumeration(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }

    public string Name { get; }

    public static IReadOnlyCollection<TEnum> List => All.Value.Values;

    public static TEnum? FromId(int id) => All.Value.GetValueOrDefault(id);

    public static TEnum? FromName(string name) =>
        All.Value.Values.FirstOrDefault(value => string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase));

    public bool Equals(Enumeration<TEnum>? other) => other is not null && other.Id == Id;

    public override bool Equals(object? obj) => obj is Enumeration<TEnum> other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => Name;

    public static bool operator ==(Enumeration<TEnum>? left, Enumeration<TEnum>? right) => Equals(left, right);

    public static bool operator !=(Enumeration<TEnum>? left, Enumeration<TEnum>? right) => !Equals(left, right);
}
