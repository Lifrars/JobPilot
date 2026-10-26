using JobPilot.Api;
using JobPilot.Api.Endpoints;
using JobPilot.Application;
using JobPilot.Infrastructure;
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

app.UseRateLimiter();

app.MapDefaultEndpoints();
app.MapEndpoints(app.MapGroup("/api/v1"));
app.MapMcp("/mcp");
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

await app.RunAsync();
