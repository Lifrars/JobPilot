using JobPilot.Domain.Abstractions;

namespace JobPilot.Domain.Users;

/// <summary>
/// Identidad del usuario dueño de los datos. El dominio solo conoce el ID: la cuenta, el password
/// y los tokens son un detalle de infraestructura (ASP.NET Core Identity).
/// </summary>
public readonly record struct UserId(Guid Value) : IStronglyTypedId<UserId, Guid>
{
    public static UserId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

/// <summary>
/// Marca un aggregate como perteneciente a un usuario. La infraestructura aplica automáticamente
/// un filtro global "solo mis datos" a todo tipo que implemente esta interfaz.
/// </summary>
public interface IOwnedByUser
{
    UserId OwnerId { get; }
}
