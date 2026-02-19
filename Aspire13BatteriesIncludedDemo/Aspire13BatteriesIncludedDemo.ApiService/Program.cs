using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.AddNpgsqlDataSource("appDb");

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

app.MapGet("/", () => "API service is running. Try /products or /weatherforecast");

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
