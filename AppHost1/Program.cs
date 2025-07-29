var builder = DistributedApplication.CreateBuilder(args);

builder.AddContainer("seq", "datalust/seq")
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithEnvironment("SEQ_FIRSTRUN_ADMINPASSWORD", "123")
    .WithHttpEndpoint(32774, targetPort: 80, name: "api")
    .WithEndpoint(32769, targetPort: 5341, name: "ingestion")
    .WithExternalHttpEndpoints()
    .WithVolume("seq-data", "/data");

builder.AddContainer("postgres", "pgvector/pgvector", tag:"pg17")
    .WithEnvironment("POSTGRES_PASSWORD", "admin")
    .WithEndpoint(3244, targetPort:5432);

builder.AddProject<Projects.AIQueryingTool>("ai-toolbox");

builder.Build().Run();