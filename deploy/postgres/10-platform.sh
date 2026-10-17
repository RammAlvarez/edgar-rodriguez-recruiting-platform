#!/bin/sh
# Same-VM Postgres, first start on an empty volume: the `platform` database and its three roles (db/roles.sql) with the
# passwords of deploy/.env. Later starts skip it (the image runs init scripts only once).
set -e
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" -c "CREATE DATABASE platform" postgres
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname platform \
  -v owner_password="$PLATFORM_OWNER_PASSWORD" -v api_password="$PLATFORM_API_PASSWORD" -v readonly_password="$PLATFORM_READONLY_PASSWORD" \
  -f /docker-entrypoint-initdb.d/roles.sql.inc
