var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL con la extensión pgvector (imagen oficial de pgvector), con volumen para no perder datos.
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg17")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("jobpilot");

builder.AddProject<Projects.JobPilot_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithExternalHttpEndpoints();

builder.Build().Run();
