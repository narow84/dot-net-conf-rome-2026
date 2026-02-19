using Aspire.Hosting;
using Aspire.Hosting.Postgres;
using Aspire13BatteriesIncludedDemo.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var pgPassword = builder.AddParameter("pg-password", secret: true)
    .WithDescription("The password for the Postgres database");

var postgres = builder.AddPostgres("postgres", password: pgPassword)
                    .WithDataVolume(isReadOnly: false)
                    .WithPgAdmin(pg => pg.WithHostPort(5051))
                    .WithLifetime(ContainerLifetime.Persistent)
                    .WithInitFiles("./postgres-init")
                    .WithCreateAppRoleCommand()
                    .WithResetDatabaseCommand("catalog-db");

var dbName = "catalog-db";
var appUserRole = "app-user";

var creationScript = $"""
    CREATE DATABASE "{dbName}"
        OWNER "{appUserRole}"
        ENCODING 'UTF8'
        LC_COLLATE 'en_US.utf8'
        LC_CTYPE 'en_US.utf8'
        TEMPLATE template0
        CONNECTION LIMIT -1;
    """;

var appDb = postgres.AddDatabase("appDb", dbName)
    .WithCreationScript(creationScript);


var apiService = builder.AddProject<Projects.Aspire13BatteriesIncludedDemo_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(appDb)
    .WaitFor(appDb);

builder.AddProject<Projects.Aspire13BatteriesIncludedDemo_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
