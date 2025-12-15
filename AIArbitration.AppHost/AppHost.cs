var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.AIArbitration_API>("aiarbitration-api");

builder.Build().Run();
