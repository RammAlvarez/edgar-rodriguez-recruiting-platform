# CLAUDE.md — Plataforma de Reclutamiento y Prenómina

Es-MX back-office platform. Read [README.md](README.md) for the layout; the authoritative docs are `docs/SPEC.md`, `docs/DATA_MODEL.md`, `docs/ARCHITECTURE.md` and `docs/PLAN.md`. The decision log is `docs/PLAN.md` §5 (D-numbers). Do not diverge from a decision silently: propose the doc change first.

## Commands

| Task | Command |
|---|---|
| Unit tests (libs / apps / API) | `npm run test:libs` · `npm run test:apps` · `npm run test:api` (API needs Postgres + the Firebase emulator up) |
| Everything | `npm test` |
| Lint (ESLint + `dotnet format --verify-no-changes`) | `npm run lint` |
| Type-check all apps | `npm run typecheck` |
| Provider neutrality | `npm run check:neutrality` |
| Pending EF model changes | `npm run check:migrations` |
| After changing API DTOs/endpoints | `npm run generate:api-types` and commit `libs/shared/src/generated` |
| Local stack | `npm run db:up && npm run db:migrate`, `npm run emulators`, `npm run api`, `npm run seed`, `npm start -- admin` |

## Ports

API 5080; admin 4300, candidates 4301, clients 4302; Postgres 5433 by default (override with `PLATFORM_DB_PORT` in `.env`); S3 emulator 9000 (`PLATFORM_S3_PORT`); Firebase auth emulator 9199, emulator UI 4010 (`firebase.json`). Another project on this machine may hold the default ports, so check `.env` before assuming.

## Rules

- **Reclutamiento and Nómina are separate businesses (D101).** A hired candidate never becomes a Personal employee: there is no «Convertir en empleado», no `candidateId` on the employee alta. Do not reintroduce it. Shared on purpose: client records, the people registry and the document vault.
- **Provider neutrality.** Vendor SDKs live only in their adapter files (Firebase in `apps/*/src/auth/firebase-*.ts` and `Platform.Api/Auth/Firebase*.cs`, S3 in `Platform.Infrastructure/Storage/`). `npm run check:neutrality` enforces it.
- **Components reach the API only through `libs/api-client`** (no `fetch`).
- **Never hand-edit** `libs/shared/src/generated/**` or EF `Migrations/`; regenerate (`generate:api-types`) or add a migration.
- **User-facing copy is Spanish (es-MX).** Identifiers and code are English. Money is a string on the wire (D46).
- **Never deploy.** Do not run `terraform apply/destroy`, `gcloud`, `firebase deploy` or `scripts/deploy-hosting.sh` (denied in `.claude/settings.json`); the DEV environment is shared with the client's testing. Ask the owner.
- **Never log secrets or email bodies** (they carry sign-in and password-setup links).
- Local DB scripts assume a local database; never point `DATABASE_URL` at a remote one while running `db:migrate`, `test:api`, `seed`, `import-excel` or `bootstrap`.

## Conventions

- Tests live next to what they test (`*.spec.ts` for Angular/libs; `services/platform-api/tests/Platform.Tests/<Area>/` for the API, with `Support/` holding `ApiFactory`, builders and fakes).
- Docs in `docs/` are the record: update them in the same change as the behavior, and note a superseded decision in the old D row.
