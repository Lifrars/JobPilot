using JobPilot.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace JobPilot.Api.Endpoints;

internal static class AuthEndpoints
{
    /// <summary>
    /// Identity API endpoints (register, login, refresh, manage/info...) más un logout,
    /// que MapIdentityApi no incluye. El frontend usa login con <c>?useCookies=true</c>:
    /// la sesión viaja en una cookie HttpOnly que JavaScript no puede leer (protege contra XSS).
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapIdentityApi<AppUser>();

        group.MapPost("/logout", async (SignInManager<AppUser> signInManager) =>
            {
                await signInManager.SignOutAsync();
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("Logout")
            .WithSummary("Cierra la sesión basada en cookie")
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }
}
