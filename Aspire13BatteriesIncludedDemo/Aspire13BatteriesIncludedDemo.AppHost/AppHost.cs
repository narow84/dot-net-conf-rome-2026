using Aspire.Hosting.Azure;
using Aspire13BatteriesIncludedDemo.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var pgPassword = builder.AddParameter("pg-password", secret: true)
    .WithDescription("The password for the Postgres database");

var postgres = builder.AddPostgres("postgres", password: pgPassword);
postgres.WithDataVolume(isReadOnly: false)
                    .WithPgAdmin(pg => pg.WithHostPort(5051)
                                         //.WithUrlForEndpoint("http", url => url.DisplayText = "🗄️ pgAdmin")
                                         .WithParentRelationship(postgres))
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


var migrations = builder.AddProject<Projects.Aspire13BatteriesIncludedDemo_MigrationService>("migrations")
    .WithReference(appDb)
    .WaitFor(appDb);

appDb.WithChildRelationship(migrations);

var aiFoundry = builder.AddAzureAIFoundry("ai-foundry");
var chatDeployment = aiFoundry.AddDeployment("chat", AIFoundryModel.OpenAI.Gpt4oMini);

var apiService = builder.AddProject<Projects.Aspire13BatteriesIncludedDemo_ApiService>("apiservice")
    .WithDevLocalhost("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(appDb)
    .WaitFor(appDb)
    .WaitForCompletion(migrations)
    .WithReference(chatDeployment)
    .WaitFor(chatDeployment)
    //.WithUrlForEndpoint("https", ep => new() { Url = "/scalar", DisplayText = "🐝 Scalar" })
    //.WithUrlForEndpoint("http", url => url.DisplayLocation= UrlDisplayLocation.DetailsOnly )
    //.WithUrlForEndpoint("https", url => url.DisplayLocation= UrlDisplayLocation.DetailsOnly )
    ;

builder.AddProject<Projects.Aspire13BatteriesIncludedDemo_Web>("webfrontend")
    .WithDevLocalhost("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    //.WithUrlForEndpoint("http", url => url.DisplayLocation= UrlDisplayLocation.DetailsOnly )
    //.WithUrlForEndpoint("https", url => url.DisplayText = "🌐 WebApp" )
    ;

builder.Build().Run();
