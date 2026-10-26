var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL con la extensión pgvector (imagen oficial de pgvector), con volumen para no perder datos.
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg17")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("jobpilot");

var api = builder.AddProject<Projects.JobPilot_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithExternalHttpEndpoints();

// Frontend Next.js (BFF): reenvía /api/v1/* a la API usando la URL que Aspire le inyecta.
// AddNextJsApp todavía es "evaluación" en Aspire 13: aceptamos el riesgo de cambios de API.
#pragma warning disable ASPIREJAVASCRIPT001
builder.AddNextJsApp("web", "../../web")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();
#pragma warning restore ASPIREJAVASCRIPT001

builder.Build().Run();
