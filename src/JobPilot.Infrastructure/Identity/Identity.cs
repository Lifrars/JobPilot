using System.Security.Claims;
using JobPilot.Application.Abstractions.Authentication;
using JobPilot.Domain.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace JobPilot.Infrastructure.Identity;

/// <summary>Cuenta de usuario de ASP.NET Core Identity. El dominio solo ve su <see cref="UserId"/>.</summary>
public sealed class AppUser : IdentityUser<Guid>;

/// <summary>
/// Implementación de <see cref="IUserContext"/> que lee el usuario del token/cookie (claim NameIdentifier).
/// También permite un modo "sistema" para procesos internos sin usuario (el Outbox), que necesitan
/// ver datos de todos los usuarios.
/// </summary>
/// <remarks>
/// Se captura el OBJETO HttpContext al crear el scope (= la request) y el usuario se lee de él cuando se necesita.
/// Dos trampas reales que esto resuelve (ver los tests de regresión en UserIsolationTests):
/// <list type="bullet">
/// <item>La factory de HybridCache corre con ThreadPool.UnsafeQueueUserWorkItem, sin ExecutionContext: ahí
/// IHttpContextAccessor.HttpContext es null. Con la referencia capturada, el usuario sigue disponible.</item>
/// <item>Con cookie, Identity construye este servicio DURANTE la autenticación (SecurityStampValidator → SignInManager
/// → DbContext → CurrentUser), cuando el usuario todavía es anónimo. Por eso no se fija el usuario en el constructor.</item>
/// </list>
/// </remarks>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private readonly HttpContext? _httpContext = httpContextAccessor.HttpContext;

    public bool IsSystem { get; private set; }

    public UserId UserId => TryGetUserId()
        ?? throw new InvalidOperationException("There is no authenticated user in the current context.");

    public UserId? TryGetUserId()
    {
        var value = (_httpContext ?? httpContextAccessor.HttpContext)?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? UserId.From(id) : null;
    }

    /// <summary>Solo para código de infraestructura confiable (nunca a partir de input del usuario).</summary>
    public void RunAsSystem() => IsSystem = true;
}
