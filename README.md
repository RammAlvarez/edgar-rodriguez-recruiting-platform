# Plataforma de Reclutamiento y Prenómina

Back-office platform (es-MX) for a Mexican recruiting and professional-services company: recruiting pipeline, client
portal, candidate documents, daily attendance, weekly pre-payroll and the bank files it ends in.

- What it does: [docs/SPEC.md](docs/SPEC.md) · data and permissions: [docs/DATA_MODEL.md](docs/DATA_MODEL.md)
- How it is built: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · slice order and decisions: [docs/PLAN.md](docs/PLAN.md)
- Tests: [docs/TEST_PLAN.md](docs/TEST_PLAN.md) · runs and defects: [docs/tests/](docs/tests/README.md)

## Layout

```
apps/admin · apps/candidates · apps/clients   Angular 22 (Material 3, RASS theme)
services/platform-api                         ASP.NET Core 10 · EF Core 10 · Postgres schema `platform`
services/scheduler                            cron → POST /internal/jobs/{name}
libs/shared · libs/api-client · libs/ui        generated wire types · fetch wrapper · shared Angular pieces
db/  deploy/  scripts/  docs/
```

## Run locally

Requirements: .NET SDK 10.0.402, Node 24, Docker, Java 21+ (Firebase emulator), `firebase-tools`.

```bash
npm ci && dotnet tool restore
# optional: a .env with PLATFORM_DB_PORT / PLATFORM_S3_PORT when 5433 / 9000 are taken (.env.example documents production vars)
npm run db:up && npm run db:migrate
npm run emulators           # separate terminal
npm run api                 # separate terminal — http://localhost:5080
npm run seed                # owner: edgar@local.test / plataforma-local
npm start -- admin          # http://localhost:4300  (candidates :4301, clients :4302)
```

### Ports

| What | Port | Override |
|---|---|---|
| API | 5080 | `ASPNETCORE_URLS` |
| Admin · candidates · clients dev servers | 4300 · 4301 · 4302 | `ng serve --port` |
| Postgres | 5433 | `PLATFORM_DB_PORT` |
| S3 emulator (MinIO) · console | 9000 · 9001 | `PLATFORM_S3_PORT` · `PLATFORM_S3_CONSOLE_PORT` |
| Firebase Auth emulator · UI | 9199 · 4010 | `firebase.json` |

Local overrides go in a gitignored `.env`; start from `.env.local.example` (`.env.example` lists the production variables).
Vocabulary: [docs/GLOSSARY.md](docs/GLOSSARY.md) · agent and contributor rules: [CLAUDE.md](CLAUDE.md).

Checks: `npm run check` (everything CI runs) · `npm run check:fast` · `npm test` · `npm run lint` · `npm run check:neutrality` · `npm run check:migrations` · `npm run generate:api-types`.
