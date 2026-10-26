using JobPilot.Domain.Users;

namespace JobPilot.Application.Abstractions.Authentication;

/// <summary>
/// Puerto: "¿quién está haciendo la petición?". Application no sabe de HTTP, claims ni tokens;
/// la implementación (Infrastructure) lo resuelve desde el usuario autenticado.
/// </summary>
public interface IUserContext
{
    /// <summary>Lanza si no hay usuario autenticado: los endpoints protegidos garantizan que exista.</summary>
    UserId UserId { get; }
}
