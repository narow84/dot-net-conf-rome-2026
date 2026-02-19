-- Runs at first container startup via /docker-entrypoint-initdb.d,
-- BEFORE Aspire's WithCreationScript creates the database.

-- Create the application role (idempotent)
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'app-user') THEN
        CREATE ROLE "app-user" WITH LOGIN PASSWORD 'app-user-password';
    END IF;
END
$$;
