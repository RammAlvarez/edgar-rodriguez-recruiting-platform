#!/bin/sh
# Local only: the postgres image runs this once on an empty volume. Creates the app database and its roles
# for both `platform` (development) and `platform_test` (xUnit, truncated between tests).
set -e
for db in platform platform_test; do
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" -c "CREATE DATABASE $db" postgres
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$db" \
    -v owner_password=platform_owner -v api_password=platform_api -v readonly_password=platform_readonly \
    -f /docker-entrypoint-initdb.d/roles.sql.inc
done
