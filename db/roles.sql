-- Database roles (ARCHITECTURE §2). Run once per database as a superuser / the provider's admin user:
--   psql "$ADMIN_DATABASE_URL" -v owner_password=... -v api_password=... -v readonly_password=... -f db/roles.sql
-- platform_owner owns schema `platform` and runs the migrations; platform_api is what the API connects as;
-- platform_readonly is for other services (read-only grants, ARCHITECTURE §2 / kickoff "Database").

\set ON_ERROR_STOP on

SELECT format('CREATE ROLE platform_owner LOGIN PASSWORD %L', :'owner_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'platform_owner') \gexec
SELECT format('CREATE ROLE platform_api LOGIN PASSWORD %L', :'api_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'platform_api') \gexec
SELECT format('CREATE ROLE platform_readonly LOGIN PASSWORD %L', :'readonly_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'platform_readonly') \gexec

-- Trusted extension used by users.email / people.email.
CREATE EXTENSION IF NOT EXISTS citext;

SELECT format('GRANT CONNECT, TEMPORARY ON DATABASE %I TO platform_api, platform_readonly', current_database()) \gexec
SELECT format('GRANT CONNECT, CREATE ON DATABASE %I TO platform_owner', current_database()) \gexec

CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION platform_owner;
GRANT USAGE ON SCHEMA platform TO platform_api, platform_readonly;

ALTER ROLE platform_owner SET search_path = platform, public;
ALTER ROLE platform_api SET search_path = platform, public;
ALTER ROLE platform_readonly SET search_path = platform, public;

-- Everything the owner creates from now on (migrations) is usable by the API, read-only for others.
-- No TRUNCATE for the API; audit_log additionally loses UPDATE/DELETE in its migration.
ALTER DEFAULT PRIVILEGES FOR ROLE platform_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO platform_api;
ALTER DEFAULT PRIVILEGES FOR ROLE platform_owner IN SCHEMA platform
    GRANT USAGE, SELECT ON SEQUENCES TO platform_api;
ALTER DEFAULT PRIVILEGES FOR ROLE platform_owner IN SCHEMA platform
    GRANT EXECUTE ON FUNCTIONS TO platform_api, platform_readonly;
ALTER DEFAULT PRIVILEGES FOR ROLE platform_owner IN SCHEMA platform
    GRANT SELECT ON TABLES TO platform_readonly;
