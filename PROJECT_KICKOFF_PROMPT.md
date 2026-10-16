# Kickoff — Plataforma de Reclutamiento y Prenómina

You are building **Plataforma de Reclutamiento y Prenómina**: a Spanish-language (es-MX) back-office platform for a Mexican recruiting and professional-services company (cleaning, maintenance, seasonal staff). It replaces five Excel workbooks: recruiters track job openings and candidates per client, clients follow their openings in a read-only portal, candidates upload their Mexican employment documents, field supervisors capture daily attendance, and the owner runs a weekly pre-payroll per client that ends in the exact bank files he uploads to Banco Azteca (massive payments), Banorte and Banregio (card requests). Users: 1 owner + ~3 office administrators (role Administrador, some with delegated grants), 6 recruiters (Reclutador), ~10 client contacts across 11 client companies (Cliente, portal only), ~500 candidates a year (Candidato, passwordless), ~150 employees on payroll.

Domain spec: `docs/SPEC.md` (behaviour, decisions D1–D32, open question Q4), `docs/DATA_MODEL.md` (entities, server rules, permission matrix in §8, reporting views, Excel import in §10). `docs/ARCHITECTURE.md` already exists (components, repository layout, API shape, jobs, deployment, decisions D33–D39) — **read it, do not rewrite it**; propose changes as doc edits. Write `docs/PLAN.md` (slice order, numbered decisions continuing at D40) before code, and keep all four docs authoritative: when code needs to diverge, propose the doc change, don't diverge silently. If the docs contradict each other or the code, stop and ask.

UI reference: `design-reference/` holds the Claude Design export of the interactive prototype (`Plataforma.dc.html`, `data.js` with the mock data and a JS version of the payroll formulas) and the **RASS Angular Material design system** in `design-reference/_ds/rass-angular-material-*/` (`readme.md` first; tokens in `tokens/`, component CSS in `components/`). Screens, flows, copy and layout come from the prototype; implement them with stock Angular Material 3 and `mat.theme()` as the design system's readme says — no custom components where Material has one. The prototype is a reference, not code to port. The client's original Excel files are in `examples/`; `examples/PRENOMINA SEM 38 12 HRS ECOVIA VALIDADORES.xlsx` is the golden test for the pre-payroll engine (SPEC §5.5: reproduce every row and the total to the cent). Ignore `archived-do-not-use/` and `QUOTE_INITIAL_SPEC.md`.

## Non-negotiable rules

**Provider-neutral.** Infrastructure is only:
- PostgreSQL over TCP;
- containers (one image per service);
- static files for the web apps;
- S3-compatible object storage (AWS SDK v3; MinIO locally).

Secrets and config are env vars. No cloud-vendor SDKs, managed queues, secret managers, cloud schedulers,
DB connectors or vendor-specific auth checks. Identity comes from **Firebase Auth** only, behind an
`IAuthProvider` interface on the server (`VerifyAsync(token) → uid`) and an `AuthProvider` on the client
(sign-in, token, sign-out). The vendor SDK may only be referenced in `Platform.Api/Auth/Firebase*.cs`
(server) and `apps/*/src/auth/firebase-*.ts` (client), and CI greps for it. Roles are **not** token
claims: they live in a `users` table; the client learns them from `GET /v1/me`.

**Everything in English** except text shown to end users (Spanish, es-MX): folders, files, tables,
columns, identifiers, env vars, commit messages.

**Layout:**
```
apps/admin            Angular 22 standalone + signals — Reclutamiento, Nómina, Usuarios (desktop-first)
apps/candidates       Angular 22 — candidate profile + documents (mobile-first, email-link sign-in)
apps/clients          Angular 22 — client portal (mobile-first, email-link sign-in)
services/platform-api ASP.NET Core 10 (.NET 10 LTS) (version in global.json), minimal APIs, FluentValidation,
                      EF Core 10 + migrations (Npgsql, snake_case), Serilog JSON logs; projects
                      Platform.Api (host, Auth/, Jobs/), Platform.Core, Platform.Recruiting,
                      Platform.Payroll, Platform.Payroll.Engine (pure, decimal), Platform.Bank
                      (Layouts/ pure + ClosedXML writers), Platform.Infrastructure (DbContext,
                      Storage/ S3, Email/, Crypto/, audit helper), tests/ (xUnit)
services/scheduler    alpine + crontab + curl → POST /internal/jobs/{name}
libs/shared           TS wire types GENERATED from the API's OpenAPI (openapi-typescript) + Zod schemas for
                      client-side form validation; the C# DTOs are the single source of shapes
libs/api-client       fetch wrapper: auth header, ISO-date reviver, ApiError, cache tags, file upload client
libs/ui               shared Angular components (tables, document checklist, money/date pipes es-MX)
db/                   docker-compose.yml (Postgres 18, MinIO, scheduler), roles.sql
deploy/               Dockerfiles, a same-VM docker-compose variant, providers/{gcp,digitalocean}.md
scripts/              local seed (through the API), production bootstrap (first admin), import-excel (one-off)
docs/                 SPEC, DATA_MODEL, ARCHITECTURE, PLAN, HANDOFF, TEST_PLAN, tests/
design-reference/     Claude Design export + RASS design system (read-only)
examples/             the client's Excel files (read-only)
```

**Database:**
- One database, **one schema per API**: `platform-api` owns schema `platform` and is its only migration owner.
  The module names in `docs/DATA_MODEL.md` (`core`, `recruiting`, `payroll`, `bank`, `audit`) are folders
  in the API, not Postgres schemas.
- Other services get read-only grants; cross-schema writes go through the owning API's internal endpoint.
- Migrations are the only way to change DDL.
- Each table has an internal `id bigint generated always as identity` primary key, never exposed, and
  `uid uuid not null unique default gen_random_uuid()`. Every foreign key references `uid`; the API
  returns `uid` as `id`. Business codes are unique columns.
- Nested value objects may be typed `jsonb` (shape = the `libs/shared` type); collections you query or
  join are rows.
- Every table has `created_at / created_by / updated_at / updated_by` stamped by the API, plus soft
  delete where the model says so.
- An append-only `audit_log` (before / after JSON, with `acting_for_user_id` for cover mode) is written in
  the same transaction as the change. A DB trigger blocks updates and deletes on it.
- Money is `numeric(12,2)` (rates `numeric(12,4)`), decimal strings on the wire, C# `decimal` on the
  server (`MidpointRounding.AwayFromZero`, once per line item); never `double`/`float` anywhere, and
  the TypeScript side never parses money into a number.

**Server logic:**
- Derived data (aggregates, counters, totals, statuses — prepayroll lines, period status, employee
  status, opening `filled`, document completeness, batch totals) is **server-only**: computed in the
  **same transaction** as the write that causes it, or computed on read when cheap. Never written by a
  client; rejected or ignored if sent.
- No background jobs for consistency.
- Scheduled work is `POST /internal/jobs/{name}`, guarded by an `INTERNAL_SECRET` header and called by
  the scheduler container. The same secret authenticates service-to-service calls.
- Jobs are idempotent and safe to re-run.

**Authorization per route:**
- From the permission matrix in `docs/DATA_MODEL.md` §8: uid from `AuthProvider.verify`, role and status
  from the users row, grants from `user_permissions`, scope (assigned openings, client) from the DB.
  Cover mode (`X-Acting-For`) only for Administrador, always audited.
- 403 carries the user-facing permission message; 400 carries the FluentValidation issues (field →
  message), so the client can show field messages.

**No realtime:**
- Every mutating route declares the cache tags it invalidates and returns them in an `X-Invalidates`
  header, on errors too.
- The api-client re-fetches the live queries tagged with them.
- An «Actualizar» (refresh) action in the shell covers changes made by other users.

**Files:**
- Uploads and downloads use presigned URLs from the API (checksum mode `WHEN_REQUIRED` for
  S3-compatible stores).
- Storage paths are decided by the API; file names are sanitized.
- Objects are private; every document view/download is written to `data_access_log`.

**Composition over inheritance:**
- No base service, base repository or base entity hierarchies.
- Shared behaviour (audit stamps, soft delete, pagination, authorization, storage) is a small injectable
  or a plain function.
- `extends` only where the framework requires it.

**Front end:**
- Data access through one repository per resource, over `libs/api-client`.
- Components never call `fetch` directly.
- No global store library; signals or local state.
- Dates travel as ISO-8601 strings; money as decimal strings, formatted "$ 1,234.56" by a pipe.
- Time zones: each payroll group has an IANA zone (`payroll_groups.time_zone`, default
  `America/Monterrey`); "today", lock times and date keys (`yyyy-MM-dd`) are computed in that zone on the
  server, never from the server's UTC clock.
- No system codes in the UI (SPEC D30): a vacante is shown as "Cliente – Puesto"; people by name.

## Testing and CI
- xUnit in the API and its projects; Vitest in the TS libs, apps and scripts.
- API tests run against a throw-away Postgres (truncated between tests) with a fake `IAuthProvider`.
  The real Firebase adapter is tested against the Firebase emulator.
- One test per permission-matrix row and per server rule; the `Platform.Payroll.Engine` golden test
  reproduces `examples/PRENOMINA SEM 38 …xlsx` row by row with `decimal`; `Platform.Bank.Layouts` tests
  reproduce the headers and validation rules of the four bank files in `examples/`.
- Browser checks follow `docs/TEST_PLAN.md`; results go in `docs/tests/YYYY-MM-DD-NN-*.md`, with a
  defect register (D-001…) in `docs/tests/README.md`.
- CI runs:
  - lint;
  - type-check;
  - all tests;
  - builds;
  - the neutrality grep (`FirebaseAdmin` only in `Platform.Api/Auth/Firebase*.cs`, Firebase JS SDK only
    in `apps/*/src/auth/firebase-*.ts`, no cloud SDKs, `AWSSDK.S3` only in
    `Platform.Infrastructure/Storage/`);
  - `dotnet ef migrations has-pending-model-changes` clean, and `npm run generate:api-types` produces
    no diff in `libs/shared`.

## Local environment
- `npm run db:up` (Postgres :5433 + MinIO + scheduler);
- `npm run db:migrate` (`dotnet ef database update`);
- the Firebase emulator (auth);
- `npm run api` (`dotnet watch` on `Platform.Api`);
- `npm start` (`--project admin | candidates | clients`);
- `npm run seed` (sample data created **through the API** so derived data is computed by the real code:
  the 11 clients, 6 recruiters, ECOVIA payroll group with VALIDADOR / SUPERVISOR rates, and the
  Semana 38 scenario from `design-reference/data.js`).

The seed refuses a non-local database. Production starts from `scripts/bootstrap` (first admin only), then
`scripts/import-excel` loads the client's workbooks once (DATA_MODEL §10).

## Deployment
Document it per provider in `deploy/providers/*.md`, all using the same images and env vars:
- a managed Postgres;
- a container service;
- static hosting;
- S3-compatible storage;
- the scheduler as a container or the host's cron;
- the same-VM docker-compose variant as the cheapest option.

No cloud commands from the agent: write the docs and scripts; a human runs them.

## Way of working
- Build in vertical slices (a schema plus its routes, rules and tests, then the screens).
- One commit per completed step (`feat:`, `fix:`, `chore:`, `docs:`, `test:`).
- Ask before anything that touches a remote (push, set-url, deploy); never force-push.
- Before starting, read the four docs, list open questions (SPEC §10 has Q4 open), and propose the slice
  order (SPEC §11 is the suggested sprint order; ARCHITECTURE §4.1 names the two signature flows).

## Project-specific inputs
- Apps: `admin` (Administrador, Reclutador — email/password, optional TOTP MFA), `candidates` (Candidato — email link), `clients` (Cliente — email link).
- APIs and their schemas: `platform-api` → `platform`.
- Auth provider: Firebase Auth; sign-in methods: email/password + optional TOTP MFA (admin app), email link (candidates, clients). Local: Firebase emulator.
- Tenancy / scoping unit: single company (the owner's); data scoped by **client** (`clients`) for the Cliente role and for client-scoped grants, and by **payroll group** (`payroll_groups`) for time zones and periods. Time zone: `America/Monterrey` default, per payroll group.
- Scheduled jobs: `attendance-lock` — every 15 min — lock attendance past each group's lock time, move finished periods to in_review · `period-roll` — daily 02:00 — create next periods · `status-roll` — daily 02:05 — expire vacation/sick-leave/suspension periods · `document-expiry` — daily 03:00 — mark expired documents, queue reminders · `notifications-digest` — daily 08:00 — send pending emails.
- External integrations: Firebase Auth (`Platform.Api/Auth/Firebase*.cs`, `apps/*/src/auth/firebase-*.ts`), Resend transactional email (`Platform.Infrastructure/Email/Resend*.cs` behind `IEmailSender`, plain HTTP), S3-compatible storage (`Platform.Infrastructure/Storage/`, AWSSDK.S3), bank file layouts as Excel generated with ClosedXML from `Platform.Bank.Layouts` (no bank APIs), PDF exports with QuestPDF.
- First provider to deploy to: Google Cloud — Cloud Run (API + scheduler containers), Cloud SQL for PostgreSQL over TCP, Firebase Hosting (3 sites), Cloud Storage through its S3 interoperability endpoint. Second: DigitalOcean. Cheapest: same-VM docker-compose.
