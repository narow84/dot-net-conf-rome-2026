using Aspire.Hosting.Azure;
using Aspire13BatteriesIncludedDemo.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Definisce l'ambiente di compute per il deploy:
// Azure Container Apps + Azure Container Registry
builder.AddAzureContainerAppEnvironment("stage");

// ─────────────────────────────────────────────────────────
// REDIS – stessa risorsa, consumata con "shape" diversi:
//   • ApiService  → Aspire.StackExchange.Redis.OutputCaching  (IOutputCacheStore)
//   • WebFrontend → Aspire.StackExchange.Redis.DistributedCaching (IDistributedCache)
// Aspire inietta la stessa connection string, ma ogni client integration
// registra il servizio .NET corretto per il driver scelto.
// ─────────────────────────────────────────────────────────
var redis = builder.AddRedis("cache")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
redis.WithRedisInsight(ri => ri.WithParentRelationship(redis));

// ─────────────────────────────────────────────────────────
// POSTGRES – stessa risorsa, consumata con "shape" diversi:
//   • ApiService       → Aspire.Npgsql                            (NpgsqlDataSource)
//   • ApiService       → Aspire.Npgsql.EntityFrameworkCore        (CatalogDbContext)
//   • MigrationService → Aspire.Npgsql                            (NpgsqlDataSource)
// Stesso ConnectionStrings:appDb → driver diversi, tipi DI diversi.
// ─────────────────────────────────────────────────────────
var pgPassword = builder.AddParameter("pg-password", secret: true)
    .WithDescription("The password for the Postgres database");

var postgres = builder.AddPostgres("postgres", password: pgPassword);
postgres.WithDataVolume(isReadOnly: false)
                    .WithPgAdmin(pg => pg.WithHostPort(5051)
                                         .WithUrlForEndpoint("http", url => url.DisplayText = "🗄️ pgAdmin")
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
    .WithReference(appDb)          // → NpgsqlDataSource + CatalogDbContext (due shape, stessa connessione)
    .WaitFor(appDb)
    .WaitForCompletion(migrations)
    .WithReference(chatDeployment)
    .WaitFor(chatDeployment)
    .WithReference(redis)           // → Redis Output Cache
    .WaitFor(redis)
    .WithUrlForEndpoint("https", ep => new() { Url = "/scalar", DisplayText = "🐝 Scalar" })
    .WithUrlForEndpoint("http", url => url.DisplayLocation= UrlDisplayLocation.DetailsOnly )
    .WithUrlForEndpoint("https", url => url.DisplayLocation= UrlDisplayLocation.DetailsOnly )
    ;

builder.AddProject<Projects.Aspire13BatteriesIncludedDemo_Web>("webfrontend")
    .WithDevLocalhost("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithReference(redis)           // → Redis Distributed Cache
    .WaitFor(redis)
    .WithUrlForEndpoint("http", url => url.DisplayLocation= UrlDisplayLocation.DetailsOnly )
    .WithUrlForEndpoint("https", url => url.DisplayText = "🌐 WebApp" )
    ;

builder.Build().Run();
