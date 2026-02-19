using System.Diagnostics;

using Npgsql;

namespace Aspire13BatteriesIncludedDemo.MigrationService;

public class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<Worker> logger) : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource s_activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = s_activitySource.StartActivity(
            "Migrating database", ActivityKind.Client);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

            await RunMigrationAsync(dataSource, stoppingToken);
            await SeedDataAsync(dataSource, stoppingToken);

            logger.LogInformation("Database migration and seeding completed successfully.");
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            logger.LogError(ex, "An error occurred while migrating the database.");
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    private static async Task RunMigrationAsync(
        NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS products (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                description TEXT,
                price NUMERIC(10,2) NOT NULL,
                created_at TIMESTAMPTZ DEFAULT NOW()
            );
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SeedDataAsync(
        NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        // Seed only if table is empty
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM products";
        var count = (long)(await countCmd.ExecuteScalarAsync(cancellationToken))!;

        if (count > 0)
            return;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO products (name, description, price) VALUES
                ('Margherita Pizza', 'Classic tomato and mozzarella', 8.50),
                ('Diavola Pizza', 'Spicy salami and chili peppers', 10.00),
                ('Carbonara', 'Pasta with guanciale, egg, and pecorino', 12.00),
                ('Tiramisù', 'Coffee-flavoured Italian dessert', 6.50),
                ('Espresso', 'Strong Italian coffee', 2.00);
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
