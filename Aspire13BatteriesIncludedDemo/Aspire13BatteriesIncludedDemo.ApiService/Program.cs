using Npgsql;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.AddNpgsqlDataSource("appDb");

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "API service is running. Try /products, /weatherforecast, or /chat?message=hello");

// --- Chat endpoint (Microsoft Agent Framework demo) ---
// The MAF agent has built-in OpenTelemetry instrumentation:
// every LLM call, tool invocation, and token usage is traced in the Aspire dashboard.

app.MapGet("/chat", async (string message, [Microsoft.Extensions.DependencyInjection.FromKeyedServices("product-assistant")] AIAgent agent) =>
{
    var response = await agent.RunAsync(message);
    return Results.Ok(new { reply = response.ToString() });
})
.WithName("Chat");

// --- Products endpoints (Postgres demo) ---

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
