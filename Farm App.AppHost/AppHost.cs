var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Farm_App>("farm-app");

builder.Build().Run();
