using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Npgsql;

namespace Aspire13BatteriesIncludedDemo.AppHost;

public static class PostgresExtensions
{
    public static IResourceBuilder<PostgresServerResource> WithCreateAppRoleCommand(
        this IResourceBuilder<PostgresServerResource> builder,
        string roleName = "app-user",
        string rolePassword = "app-user-password")
    {
        return builder.WithCommand(
            name: "create-app-role",
            displayName: "Create App Role",
            executeCommand: async context =>
            {
                var connectionString = await builder.Resource.GetConnectionStringAsync(context.CancellationToken)
                                       + ";Database=postgres;";

                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(context.CancellationToken);

                await using var cmd = connection.CreateCommand();
                cmd.CommandText = $"""
                    DO $$
                    BEGIN
                        IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{roleName}') THEN
                            CREATE ROLE "{roleName}" WITH LOGIN PASSWORD '{rolePassword}';
                        END IF;
                    END
                    $$;
                    """;
                await cmd.ExecuteNonQueryAsync(context.CancellationToken);

                return CommandResults.Success();
            },
            commandOptions: new CommandOptions
            {
                Description = $"Creates the '{roleName}' role in the PostgreSQL instance (idempotent).",
                IconName = "PersonAdd",
                ConfirmationMessage = $"Create the '{roleName}' role in the PostgreSQL instance?"
            });
    }

    public static IResourceBuilder<PostgresServerResource> WithResetDatabaseCommand(
        this IResourceBuilder<PostgresServerResource> builder,
        string databaseName,
        string ownerRole = "app-user")
    {
        return builder.WithCommand(
            name: "reset-database",
            displayName: "Reset Database",
            executeCommand: async context =>
            {
                var connectionString = await builder.Resource.GetConnectionStringAsync(context.CancellationToken)
                                       + ";Database=postgres;";

                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(context.CancellationToken);

                // Terminate existing connections to the database
                await using (var terminateCmd = connection.CreateCommand())
                {
                    terminateCmd.CommandText = $"""
                        SELECT pg_terminate_backend(pid)
                        FROM pg_stat_activity
                        WHERE datname = '{databaseName}' AND pid <> pg_backend_pid();
                        """;
                    await terminateCmd.ExecuteNonQueryAsync(context.CancellationToken);
                }

                // Drop and recreate
                await using (var dropCmd = connection.CreateCommand())
                {
                    dropCmd.CommandText = $"""DROP DATABASE IF EXISTS "{databaseName}";""";
                    await dropCmd.ExecuteNonQueryAsync(context.CancellationToken);
                }

                await using (var createCmd = connection.CreateCommand())
                {
                    createCmd.CommandText = $"""
                        CREATE DATABASE "{databaseName}"
                            OWNER "{ownerRole}"
                            ENCODING 'UTF8'
                            LC_COLLATE 'en_US.utf8'
                            LC_CTYPE 'en_US.utf8'
                            TEMPLATE template0
                            CONNECTION LIMIT -1;
                        """;
                    await createCmd.ExecuteNonQueryAsync(context.CancellationToken);
                }

                return CommandResults.Success();
            },
            commandOptions: new CommandOptions
            {
                Description = $"Drops and recreates the '{databaseName}' database. All data will be lost!",
                IconName = "DatabaseArrowDown",
                ConfirmationMessage = $"This will DROP and recreate '{databaseName}'. All data will be lost. Continue?"
            });
    }
}
