var builder = DistributedApplication.CreateBuilder(args);

builder.AddContainer("seq", "datalust/seq")
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithEnvironment("SEQ_PASSWORD", "admin123")
    .WithEndpoint(32768, targetPort: 80, name: "api")
    .WithEndpoint(32769, targetPort: 5341, name: "ingestion")
    .WithVolume("seq-data", "/data");

builder.AddContainer("postgres", "pgvector/pgvector", tag:"pg17")
    .WithEnvironment("POSTGRES_PASSWORD", "admin")
    .WithEndpoint(32774, targetPort:5432);

builder.AddProject<Projects.AIQueryingTool>("ai-toolbox");

builder.Build().Run();