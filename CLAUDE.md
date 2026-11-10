# CLAUDE.md — Plataforma de Reclutamiento y Prenómina

Es-MX back-office platform. Read [README.md](README.md) for the layout; the authoritative docs are `docs/SPEC.md`, `docs/DATA_MODEL.md`, `docs/ARCHITECTURE.md` and `docs/PLAN.md`. The decision log is `docs/PLAN.md` §5 (D-numbers). Do not diverge from a decision silently: propose the doc change first.

## Commands

| Task | Command |
|---|---|
| Unit tests (libs / apps / API) | `npm run test:libs` · `npm run test:apps` · `npm run test:api` (API needs Postgres + the Firebase emulator up) |
| Fast check after an edit (neutrality, lint, lib types, Vitest) | `npm run check:fast` |
| Everything CI runs (needs the local stack up) | `npm run check` |
| Tests only | `npm test` |
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
- **Initial bundles have a 1 MB budget (D62).** Files in the initial bundle (`app.routes.ts`, `app.config.ts`, `shell/*`) import `@platform/ui/<file>` (e.g. `@platform/ui/session`), never the `@platform/ui` barrel, which drags Material and every panel in. Lazy screens may use the barrel. `npm run typecheck` does not enforce the budget; `npm run build:apps` does.
- **Never hand-edit** `libs/shared/src/generated/**` or EF `Migrations/`; regenerate (`generate:api-types`) or add a migration.
- **User-facing copy is Spanish (es-MX).** Identifiers and code are English; `docs/GLOSSARY.md` maps one to the other (add new terms there). Money is a string on the wire (D46).
- **Never deploy.** Do not run `terraform apply/destroy`, `gcloud`, `firebase deploy` or `scripts/deploy-hosting.sh` (denied in `.claude/settings.json`); the DEV environment is shared with the client's testing. Ask the owner.
- **Never log secrets or email bodies** (they carry sign-in and password-setup links).
- Local DB scripts assume a local database: `scripts/run.mjs` (`api`, `db:migrate`, `test:api`) refuses a non-local `DATABASE_URL`, `MIGRATIONS_DATABASE_URL`, `TEST_DATABASE_URL` or `TEST_MIGRATIONS_DATABASE_URL`, `seed` refuses one too, and `bootstrap` / `import-excel` need `--yes` for a non-local `API_URL`. Do not work around these guards.

## Conventions

- Tests live next to what they test (`*.spec.ts` for Angular/libs; `services/platform-api/tests/Platform.Tests/<Area>/` for the API, with `Support/` holding `ApiFactory`, builders and fakes).
- Docs in `docs/` are the record: update them in the same change as the behavior, and note a superseded decision in the old D row.

## Recipes

**New API endpoint** (`services/platform-api/src/Platform.<Module>/<Area>/<Thing>Endpoints.cs`)
1. Copy the shape of `IncidentEndpoints.cs`: a static class with `Tag`, `Access = AccessRule.Administrator(PermissionModule.X)` and `Map<Thing>Endpoints(this IEndpointRouteBuilder v1)` using `v1.MapGroup(...)`.
2. Every route gets `.WithName`, `.Produces<…>` and `.RequireAccess(Access)`; mutating routes also `.Invalidates(Tag, …)` / `.Mutation()`. `RouteConventionTests` fails a route without exactly one access rule or a mutation without cache tags.
3. Scope every query to the caller's grant (`PayrollScope` for payroll): out of scope answers 404, not 403. Validate with a FluentValidation validator (Spanish messages). Write audit rows in the same transaction as the change.
4. Register `v1.Map<Thing>Endpoints()` in `Platform.Api/Program.cs`, then `npm run generate:api-types` and commit `libs/shared/src/generated`.
5. Tests in `services/platform-api/tests/Platform.Tests/<Area>/` (use `ApiTest`, `SignedIn`, `Builders`). For a mutating payroll route (Prenómina, Incidencias, Asistencia, Personal, Horarios, Grupos) also add a line to `PayrollScopedWriteTests`; the Bank routes are not covered yet.

**New migration**
1. Change the entity or its configuration; never hand-edit `Migrations/` or the model snapshot.
2. `node scripts/run.mjs dotnet ef migrations add <Name> --project services/platform-api/src/Platform.Infrastructure --startup-project services/platform-api/src/Platform.Infrastructure`
3. `npm run db:migrate`, then `npm run check:migrations` (no pending model changes) and `npm run test:api`.

**New admin screen**
1. Component in `apps/admin/src/app/<area>/<name>-page.ts`; data through a repository in `apps/admin/src/app/data/` (`API_CLIENT` + `liveQuery`), never `fetch`.
2. Add a `NavItem` (`path`, `label`, `icon`, `roles`, optional `grant`) to `shell/navigation.ts` and the lazy import to `SCREENS` in `app.routes.ts`; the route guard follows the item's roles and grant.
3. Cover visibility in `shell/navigation.spec.ts`.

**New form or side panel**
1. Use `PanelState` from `@platform/ui` (busy flag, 400 → field errors, alert line) and `openSidePanel`; for actions outside a form use `runExclusive`.
2. Toasts say `apiMessage(e, 'fallback')` / `apiFieldMessage(e, 'fallback')`; copy is es-MX.
3. Cover a shared guard or helper with the harness in `libs/ui/src/testing/guard-harness.ts` (run through an app's `ng test`).
