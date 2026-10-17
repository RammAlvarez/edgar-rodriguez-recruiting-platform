# DigitalOcean

Two ways, same images and variables:

- **App Platform** (managed, no servers): below.
- **One Droplet with Docker Compose**: [same-vm.md](./same-vm.md), with Managed PostgreSQL instead of the Postgres container if
  you want the provider's backups (set `DATABASE_URL` / `MIGRATIONS_DATABASE_URL` to it and remove the `postgres` service).

| Piece | Service |
|---|---|
| Postgres | Managed PostgreSQL 18, smallest node, daily backups + point-in-time recovery (7 days) |
| API | App Platform **service** from `services/platform-api/Dockerfile`, HTTP port 8080, route `/` on `api.<domain>` |
| Migrations | App Platform **pre-deploy job**, same Dockerfile, run command `./efbundle` |
| Scheduler | App Platform **worker** from `services/scheduler` (no route) |
| Apps | three App Platform **static sites** (build command `npx ng build <app>`, output `dist/<app>/browser`) |
| Storage | Spaces, two buckets |

## 1. Database

Create the cluster (Databases → PostgreSQL 18), the database `platform`, and from the console's connection string run
`db/roles.sql` once as `doadmin` (`psql "$URL" -v owner_password=… -v api_password=… -v readonly_password=… -f db/roles.sql`).
Add the App Platform app as a trusted source. URLs need `?sslmode=require`.

## 2. Spaces

Two Spaces (private) in the same region, e.g. `platform-documents` and `platform-exports`; a Spaces access key;
`S3_ENDPOINT=https://<region>.digitaloceanspaces.com`, `S3_REGION=us-east-1`, `S3_FORCE_PATH_STYLE=false`. CORS on
documents for the app origins (`PUT`, `GET`, header `Content-Type`); a lifecycle rule (90 days) on exports with
`s3cmd setlifecycle` or the API; versioning on documents.

## 3. The app (App Platform spec)

Create the app from the repository with:
- service `api`: Dockerfile `services/platform-api/Dockerfile`, source dir `/`, port 8080, env vars of `.env.example` as
  **encrypted** variables (`ENCRYPTION_KEY`, `INTERNAL_SECRET`, `FIREBASE_SERVICE_ACCOUNT_JSON`, `RESEND_API_KEY`, the S3 keys,
  `DATABASE_URL`), health check path `/health`;
- job `migrate`, kind **PRE_DEPLOY**, same Dockerfile, run command `./efbundle`, env `MIGRATIONS_DATABASE_URL` (owner role);
- worker `scheduler`: Dockerfile `services/scheduler/Dockerfile`, env `API_URL=${api.PRIVATE_URL}`, `INTERNAL_SECRET`;
- static sites `admin`, `candidatos`, `clientes`: build `npm ci && npx ng build <app> && cp $CONFIG_JSON dist/<app>/browser/config.json`
  (or commit a per-environment config through a build-time variable), output `dist/<app>/browser`, catch-all document
  `index.html`.

Block `/internal` from the public route if your plan allows a path rule; otherwise it is protected by `INTERNAL_SECRET`.
Domains: add `admin.`, `candidatos.`, `clientes.`, `api.` to the app (App Platform issues the certificates), then to
Firebase Auth → Authorized domains and to `APP_ORIGINS`.

## 4. After

HANDOFF §3 steps 7–10, uptime alert on `/health`, and the quarterly restore drill (HANDOFF §7: fork the database cluster
from a backup and run one API container against it).
