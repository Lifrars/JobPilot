using JobPilot.Api;
using JobPilot.Api.Endpoints;
using JobPilot.Application;
using JobPilot.Infrastructure;
using JobPilot.Infrastructure.Identity;
using JobPilot.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Composition root: cada capa aporta sus registros.
builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
    {
        await app.Services.ApplyMigrationsAsync();
    }
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapDefaultEndpoints();

// Público: registro, login y refresh de tokens (Identity API endpoints).
app.MapGroup("/api/v1/auth").WithTags("Auth").MapIdentityApi<AppUser>();

// Todo lo demás exige un usuario autenticado; los query filters aíslan sus datos.
app.MapEndpoints(app.MapGroup("/api/v1").RequireAuthorization());
app.MapMcp("/mcp").RequireAuthorization();

app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

await app.RunAsync();
