using System.Security.Claims;
using JobPilot.Application.Abstractions.Authentication;
using JobPilot.Domain.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace JobPilot.Infrastructure.Identity;

/// <summary>Cuenta de usuario de ASP.NET Core Identity. El dominio solo ve su <see cref="UserId"/>.</summary>
public sealed class AppUser : IdentityUser<Guid>;

/// <summary>
/// Implementación de <see cref="IUserContext"/> que lee el usuario del token (claim NameIdentifier).
/// También permite un modo "sistema" para procesos internos sin usuario (el Outbox), que necesitan
/// ver datos de todos los usuarios.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public bool IsSystem { get; private set; }

    public UserId UserId => TryGetUserId()
        ?? throw new InvalidOperationException("There is no authenticated user in the current context.");

    public UserId? TryGetUserId()
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? UserId.From(id) : null;
    }

    /// <summary>Solo para código de infraestructura confiable (nunca a partir de input del usuario).</summary>
    public void RunAsSystem() => IsSystem = true;
}
