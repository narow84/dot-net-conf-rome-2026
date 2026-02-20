using Npgsql;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Scalar.AspNetCore;
using StackExchange.Redis;
using Aspire13BatteriesIncludedDemo.ApiService.Data;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// ─────────────────────────────────────────────────────────
// CONNECTION SHAPING: PostgreSQL "appDb" → due driver diversi
// ─────────────────────────────────────────────────────────
// Shape 1: Aspire.Npgsql → registra NpgsqlDataSource (raw ADO.NET)
builder.AddNpgsqlDataSource("appDb");

// Shape 2: Aspire.Npgsql.EntityFrameworkCore.PostgreSQL → registra CatalogDbContext (EF Core ORM)
// STESSA connection string "appDb", ma servizio DI diverso!
builder.AddNpgsqlDbContext<CatalogDbContext>("appDb");

// ─────────────────────────────────────────────────────────
// CONNECTION SHAPING: Redis "cache" → Output Cache provider
// ─────────────────────────────────────────────────────────
// Shape: Aspire.StackExchange.Redis.OutputCaching → configura l'output cache middleware
// Nel Web frontend, la stessa risorsa "cache" viene consumata come IDistributedCache.
builder.AddRedisOutputCache("cache");

// Add Azure AI Foundry Chat Completions client + register keyed IChatClient.
// Connection name "chat" matches the deployment resource name from the AppHost.
builder.AddAzureChatCompletionsClient("chat")
    .AddKeyedChatClient("chat");

// Register MAF agent with tool calling + built-in OpenTelemetry instrumentation.
builder.AddAIAgent(
    name: "product-assistant",
    instructions: """
        You are a helpful assistant for an online product catalog.
        You can search for products and provide details about them.
        Always respond in a friendly and concise manner.
        When asked about products, use the available tools to look up real data.
        """,
    description: "An AI assistant that helps with product catalog queries.",
    chatClientServiceKey: "chat")
    .WithAITool(sp =>
    {
        var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
        return AIFunctionFactory.Create(async (string? searchTerm) =>
        {
            await using var conn = await dataSource.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                cmd.CommandText = "SELECT id, name, description, price FROM products ORDER BY id LIMIT 10";
            }
            else
            {
                cmd.CommandText = "SELECT id, name, description, price FROM products WHERE name ILIKE $1 OR description ILIKE $1 ORDER BY id LIMIT 10";
                cmd.Parameters.AddWithValue($"%{searchTerm}%");
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            var products = new List<object>();
            while (await reader.ReadAsync())
            {
                products.Add(new
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Price = reader.GetDecimal(3)
                });
            }
            return products;
        }, "SearchProducts", "Searches the product catalog. If searchTerm is provided, filters by name or description. Returns up to 10 products.");
    });

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Abilita l'output cache middleware (backed by Redis grazie al connection shaping)
app.UseOutputCache();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/", () => "API service is running. Try /products, /products/ef, /diagnostics/connection-shaping, /weatherforecast, or /chat?message=hello");

// --- Chat endpoint (Microsoft Agent Framework demo) ---
// The MAF agent has built-in OpenTelemetry instrumentation:
// every LLM call, tool invocation, and token usage is traced in the Aspire dashboard.

app.MapGet("/chat", async (string message, [Microsoft.Extensions.DependencyInjection.FromKeyedServices("product-assistant")] AIAgent agent) =>
{
    var response = await agent.RunAsync(message);
    return Results.Ok(new { reply = response.ToString() });
})
.WithName("Chat");

// ───────────────────────────────────────────────────────────
// Products endpoints – SHAPE 1: raw NpgsqlDataSource (ADO.NET)
// Aspire ha iniettato la connection string e registrato NpgsqlDataSource
// ───────────────────────────────────────────────────────────

app.MapGet("/products", async (NpgsqlDataSource dataSource) =>
{
    await using var conn = await dataSource.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, name, description, price, created_at FROM products ORDER BY id";
    await using var reader = await cmd.ExecuteReaderAsync();

    var products = new List<Product>();
    while (await reader.ReadAsync())
    {
        products.Add(new Product(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetDateTime(4)
        ));
    }
    return Results.Ok(products);
})
.CacheOutput(p => p.Expire(TimeSpan.FromSeconds(30))) // Cached in Redis tramite output cache shaping
.WithName("GetProducts");

app.MapGet("/products/{id:int}", async (int id, NpgsqlDataSource dataSource) =>
{
    await using var conn = await dataSource.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, name, description, price, created_at FROM products WHERE id = $1";
    cmd.Parameters.AddWithValue(id);
    await using var reader = await cmd.ExecuteReaderAsync();

    if (!await reader.ReadAsync())
        return Results.NotFound();

    var product = new Product(
        reader.GetInt32(0),
        reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.GetDecimal(3),
        reader.GetDateTime(4)
    );
    return Results.Ok(product);
})
.WithName("GetProductById");

app.MapPost("/products", async (CreateProductRequest request, NpgsqlDataSource dataSource) =>
{
    await using var conn = await dataSource.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        INSERT INTO products (name, description, price)
        VALUES ($1, $2, $3)
        RETURNING id, name, description, price, created_at
        """;
    cmd.Parameters.AddWithValue(request.Name);
    cmd.Parameters.AddWithValue(request.Description ?? (object)DBNull.Value);
    cmd.Parameters.AddWithValue(request.Price);
    await using var reader = await cmd.ExecuteReaderAsync();
    await reader.ReadAsync();

    var product = new Product(
        reader.GetInt32(0),
        reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.GetDecimal(3),
        reader.GetDateTime(4)
    );
    return Results.Created($"/products/{product.Id}", product);
})
.WithName("CreateProduct");

app.MapDelete("/products/{id:int}", async (int id, NpgsqlDataSource dataSource) =>
{
    await using var conn = await dataSource.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "DELETE FROM products WHERE id = $1";
    cmd.Parameters.AddWithValue(id);
    var rows = await cmd.ExecuteNonQueryAsync();
    return rows > 0 ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteProduct");

// ─────────────────────────────────────────────────────────
// Products endpoint – SHAPE 2: Entity Framework Core (CatalogDbContext)
// Stessa risorsa "appDb", ma Aspire l'ha shapata come DbContext EF Core.
// ─────────────────────────────────────────────────────────

app.MapGet("/products/ef", async (CatalogDbContext db) =>
{
    var products = await db.Products
        .OrderBy(p => p.Id)
        .Select(p => new Product(p.Id, p.Name, p.Description, p.Price, p.CreatedAt))
        .ToListAsync();
    return Results.Ok(products);
})
.CacheOutput(p => p.Expire(TimeSpan.FromSeconds(30)))
.WithName("GetProductsEfCore");

app.MapGet("/products/ef/{id:int}", async (int id, CatalogDbContext db) =>
{
    var p = await db.Products.FindAsync(id);
    return p is null
        ? Results.NotFound()
        : Results.Ok(new Product(p.Id, p.Name, p.Description, p.Price, p.CreatedAt));
})
.WithName("GetProductByIdEfCore");

// ─────────────────────────────────────────────────────────
// DIAGNOSTICS – mostra come Aspire ha shapato le connessioni
// ─────────────────────────────────────────────────────────

app.MapGet("/diagnostics/connection-shaping", (
    NpgsqlDataSource npgsqlDs,
    CatalogDbContext dbContext,
    IConnectionMultiplexer redis,
    IConfiguration config) =>
{
    static string Sanitize(string? cs)
    {
        if (string.IsNullOrEmpty(cs)) return "(not available)";
        return System.Text.RegularExpressions.Regex.Replace(
            cs, @"(?i)(password|pwd)=[^;]+", "$1=***");
    }

    // ── La pipeline in 3 step per ogni "shape" ──
    // Step 1 (AppHost)   → definisci la risorsa: AddPostgres/AddRedis
    // Step 2 (AppHost)   → collega ai progetti:  WithReference(resource)
    //                       Aspire genera ConnectionStrings__<name> come ENV VAR nel processo
    // Step 3 (Progetto)  → scegli il driver:     AddNpgsqlDataSource / AddNpgsqlDbContext / AddRedisOutputCache
    //                       Il client integration legge ConnectionStrings:<name> dall'IConfiguration
    //                       IConfiguration capisce che ConnectionStrings__appDb (env var) = ConnectionStrings:appDb (config key)
    //                       ZERO modifiche a codice o appsettings.json — è tutto env var!

    var appDbRaw = Sanitize(config.GetConnectionString("appDb"));
    var cacheRaw = Sanitize(config.GetConnectionString("cache"));

    // Leggi le env var reali dal processo — queste SONO la magia
    // Aspire usa __ come separatore (convenzione .NET per env var gerarchiche)
    var envVars = new Dictionary<string, string>();
    foreach (var entry in Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>())
    {
        var k = entry.Key?.ToString() ?? "";
        if (k.StartsWith("ConnectionStrings__", StringComparison.OrdinalIgnoreCase))
        {
            envVars[k] = Sanitize(entry.Value?.ToString());
        }
    }

    // Raccogli anche le env var di service discovery (per mostrare il meccanismo completo)
    foreach (var entry in Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>())
    {
        var k = entry.Key?.ToString() ?? "";
        if (k.StartsWith("services__", StringComparison.OrdinalIgnoreCase))
        {
            envVars[k] = entry.Value?.ToString() ?? "";
        }
    }

    return Results.Ok(new
    {
        Title = "🔌 Aspire Connection Shaping — Dove avviene la magia",

        // ── IL PUNTO FONDAMENTALE: env var = zero codice, zero appsettings.json ──
        EnvVar_Mechanism = new
        {
            Title = "🎯 Perché env var? Zero sforzo, zero modifiche di codice",
            How = new[]
            {
                "1. L'AppHost chiama .WithReference(appDb) — questo NON modifica codice del progetto consumer",
                "2. Aspire genera una ENV VAR nel processo del progetto: ConnectionStrings__appDb=Host=...;Database=catalog-db;...",
                "3. Il .NET Configuration system mappa automaticamente ConnectionStrings__appDb → ConnectionStrings:appDb",
                "4. builder.AddNpgsqlDataSource(\"appDb\") chiama config.GetConnectionString(\"appDb\") — e trova il valore!",
                "5. → NESSUN appsettings.json da mantenere, NESSUN User Secret, NESSUN file di configurazione"
            },
            Why_This_Is_Brilliant = new[]
            {
                "✅ Cambi infrastruttura (password, host, porta)? → Solo l'AppHost cambia, i progetti NON vengono toccati",
                "✅ Aggiungi un nuovo servizio consumer? → Basta .WithReference() nell'AppHost, il progetto sceglie il suo driver",
                "✅ Dev locale vs Produzione? → Aspire genera env var diverse (container locale vs Azure Managed Identity), il codice è IDENTICO",
                "✅ Nessun rischio di secret committed nel repo — le env var sono effimere, vivono solo nel processo"
            },
            DotNet_Convention = "ENV VAR con __ (doppio underscore) → IConfiguration key con : (due punti). Es: ConnectionStrings__appDb → ConnectionStrings:appDb"
        },

        // ── Le ENV VAR reali iniettate nel processo di questo ApiService ──
        Injected_EnvVars = envVars.OrderBy(kv => kv.Key).Select(kv => new
        {
            EnvVarName = kv.Key,
            Value = kv.Value,
            MapsTo_IConfiguration = kv.Key.Replace("__", ":")
        }),

        Pipeline = new[]
        {
            // ───────────── PostgreSQL: Shape 1 (Npgsql raw) ─────────────
            new
            {
                Resource = "PostgreSQL → appDb",
                Step1_AppHost_Resource = "builder.AddPostgres(\"postgres\").AddDatabase(\"appDb\", \"catalog-db\")",
                Step1_File = "AppHost.cs",
                Step2_AppHost_Reference = ".WithReference(appDb)  // su apiservice E migrations",
                Step2_WhatHappens = "Aspire inietta ENV VAR → ConnectionStrings__appDb=Host=...;Database=catalog-db;... — NESSUNA modifica a codice o config",
                Step2_EnvVarName = "ConnectionStrings__appDb",
                Step2_InjectedValue = appDbRaw,
                Step3_ClientIntegration = "builder.AddNpgsqlDataSource(\"appDb\")",
                Step3_NuGetPackage = "Aspire.Npgsql",
                Step3_File = "ApiService/Program.cs (riga 20)",
                Step3_WhatHappens = "Chiama config.GetConnectionString(\"appDb\") → .NET traduce in env var ConnectionStrings__appDb → crea NpgsqlDataSource → Singleton nel DI",
                Step3_RegisteredType = "NpgsqlDataSource",
                Step3_UsedVia = "NpgsqlDataSource dataSource  (parameter injection negli endpoint)",
                ProofConnectionString = Sanitize(npgsqlDs.ConnectionString),
                Endpoint = "/products"
            },
            // ───────────── PostgreSQL: Shape 2 (EF Core) ─────────────
            new
            {
                Resource = "PostgreSQL → appDb",
                Step1_AppHost_Resource = "builder.AddPostgres(\"postgres\").AddDatabase(\"appDb\", \"catalog-db\")",
                Step1_File = "AppHost.cs",
                Step2_AppHost_Reference = ".WithReference(appDb)  // stesso .WithReference di prima!",
                Step2_WhatHappens = "STESSA env var ConnectionStrings__appDb — zero configurazione aggiuntiva, zero file modificati",
                Step2_EnvVarName = "ConnectionStrings__appDb",
                Step2_InjectedValue = appDbRaw,
                Step3_ClientIntegration = "builder.AddNpgsqlDbContext<CatalogDbContext>(\"appDb\")",
                Step3_NuGetPackage = "Aspire.Npgsql.EntityFrameworkCore.PostgreSQL",
                Step3_File = "ApiService/Program.cs (riga 24)",
                Step3_WhatHappens = "Chiama config.GetConnectionString(\"appDb\") → stessa env var! → ma registra DbContext<CatalogDbContext> invece di NpgsqlDataSource",
                Step3_RegisteredType = "CatalogDbContext (DbContext<CatalogDbContext>)",
                Step3_UsedVia = "CatalogDbContext db  (parameter injection negli endpoint)",
                ProofConnectionString = Sanitize(dbContext.Database.GetConnectionString()),
                Endpoint = "/products/ef"
            },
            // ───────────── Redis: Shape OutputCache (ApiService) ─────────────
            new
            {
                Resource = "Redis → cache",
                Step1_AppHost_Resource = "builder.AddRedis(\"cache\")",
                Step1_File = "AppHost.cs",
                Step2_AppHost_Reference = ".WithReference(redis)  // su apiservice",
                Step2_WhatHappens = "Aspire inietta ENV VAR → ConnectionStrings__cache=localhost:port — zero modifica a codice o config",
                Step2_EnvVarName = "ConnectionStrings__cache",
                Step2_InjectedValue = cacheRaw,
                Step3_ClientIntegration = "builder.AddRedisOutputCache(\"cache\")",
                Step3_NuGetPackage = "Aspire.StackExchange.Redis.OutputCaching",
                Step3_File = "ApiService/Program.cs (riga 31)",
                Step3_WhatHappens = "Chiama config.GetConnectionString(\"cache\") → env var ConnectionStrings__cache → IConnectionMultiplexer + OutputCache middleware",
                Step3_RegisteredType = "IOutputCacheStore (+ IConnectionMultiplexer)",
                Step3_UsedVia = "app.UseOutputCache() + .CacheOutput() sui singoli endpoint",
                ProofConnectionString = redis.Configuration ?? "(multiplexer connected)",
                Endpoint = "/products (con header Cache-Control)"
            },
            // ───────────── Redis: Shape DistributedCache (Web) ─────────────
            new
            {
                Resource = "Redis → cache",
                Step1_AppHost_Resource = "builder.AddRedis(\"cache\")",
                Step1_File = "AppHost.cs",
                Step2_AppHost_Reference = ".WithReference(redis)  // su webfrontend",
                Step2_WhatHappens = "STESSA env var ConnectionStrings__cache — ma iniettata nel processo del Web frontend",
                Step2_EnvVarName = "ConnectionStrings__cache",
                Step2_InjectedValue = cacheRaw,
                Step3_ClientIntegration = "builder.AddRedisDistributedCache(\"cache\")",
                Step3_NuGetPackage = "Aspire.StackExchange.Redis.DistributedCaching",
                Step3_File = "Web/Program.cs (riga 15)",
                Step3_WhatHappens = "Chiama config.GetConnectionString(\"cache\") → stessa env var! → ma registra IDistributedCache (RedisCache) nel DI",
                Step3_RegisteredType = "IDistributedCache",
                Step3_UsedVia = "@inject IDistributedCache DistributedCache  (nei componenti Razor)",
                ProofConnectionString = cacheRaw,
                Endpoint = "/connection-shaping (pagina Web)"
            }
        },
        Key_Insight = new
        {
            Punto1 = "Le connection string sono ENV VAR (ConnectionStrings__<name>) — il codice del progetto NON cambia MAI.",
            Punto2 = ".NET Configuration mappa __ → : automaticamente. config.GetConnectionString(\"appDb\") legge ConnectionStrings__appDb dall'ambiente.",
            Punto3 = "WithReference() nell'AppHost è l'UNICA cosa che serve — genera l'env var e la inietta nel processo. Zero appsettings.json, zero User Secrets.",
            Punto4 = "La 'magia' è nel builder.Add*() della client integration: legge l'env var via IConfiguration, crea il tipo .NET giusto, lo registra nel DI. Il dev NON tocca nulla.",
            Punto5 = "Dev locale ↔ Produzione: Aspire genera env var diverse (localhost:random_port vs Azure connection string con Managed Identity). Il codice è IDENTICO."
        },
        RawConnectionStrings = new
        {
            appDb = appDbRaw,
            cache = cacheRaw
        }
    });
})
.WithName("GetConnectionShaping");

// --- Weather forecast endpoint ---

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapDefaultEndpoints();

app.Run();

// --- Models ---

record Product(int Id, string Name, string? Description, decimal Price, DateTime CreatedAt);
record CreateProductRequest(string Name, string? Description, decimal Price);

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
