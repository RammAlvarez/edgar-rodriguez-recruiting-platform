# Kickoff — {PROJECT_NAME}

You are building {PROJECT_NAME}: {ONE_PARAGRAPH_WHAT_IT_DOES}. Users: {ROLES_AND_ROUGH_COUNTS}.
Domain spec: `docs/SPEC.md` (behaviour), `docs/DATA_MODEL.md` (entities, server rules, permission matrix).
Write `docs/ARCHITECTURE.md` and `docs/PLAN.md` (numbered decisions D1…Dn, slice order) before code, and
keep all four docs authoritative: when code needs to diverge, propose the doc change, don't diverge silently.
If the docs contradict each other or the code, stop and ask.

## Non-negotiable rules

**Provider-neutral.** Infrastructure is only:
- PostgreSQL over TCP;
- containers (one image per service);
- static files for the web apps;
- S3-compatible object storage (AWS SDK v3; MinIO locally).

Secrets and config are env vars. No cloud-vendor SDKs, managed queues, secret managers, cloud schedulers,
DB connectors or vendor-specific auth checks. Identity comes from {AUTH_PROVIDER} only, behind an
`AuthProvider` interface on the server (`verify(token) → uid`) and on the client (sign-in, token, sign-out).
The vendor SDK may only be imported in `*/auth/{vendor}-*.ts`, and CI greps for it. Roles are **not** token
claims: they live in a `users` table; the client learns them from `GET /v1/me`.

**Everything in English** except text shown to end users ({UI_LANGUAGE}): folders, files, tables,
columns, identifiers, env vars, commit messages.

**Layout:**
```
apps/{app}            web SPA(s): {FRONTEND_STACK, e.g. Angular standalone + signals | React + Vite}
services/{name}-api   API(s): Node (version in .nvmrc), TypeScript, NestJS on Fastify, Zod validation,
                      Drizzle ORM + drizzle-kit migrations, pino JSON logs
services/scheduler    alpine + crontab + curl → POST /internal/jobs/{name}
libs/shared           wire types + Zod schemas shared by apps and APIs (the single source of shapes)
libs/api-client       fetch wrapper: auth header, ISO-date reviver, ApiError, cache tags, file upload client
libs/{domain}         pure domain logic (calculations), no I/O, unit-tested
db/                   docker-compose.yml (Postgres 18, MinIO, scheduler), roles.sql
deploy/               Dockerfiles, a same-VM docker-compose variant, providers/{gcp,digitalocean,…}.md
scripts/              local seed (through the API), production bootstrap (first admin), one-off tools
docs/                 SPEC, DATA_MODEL, ARCHITECTURE, PLAN, HANDOFF, TEST_PLAN, tests/
```

**Database:**
- One database, **one schema per API**. Each schema has exactly one migration owner (its API).
- Other services get read-only grants; cross-schema writes go through the owning API's internal endpoint.
- Migrations are the only way to change DDL.
- Each table has an internal `id bigint generated always as identity` primary key, never exposed, and
  `uid uuid not null unique default gen_random_uuid()`. Every foreign key references `uid`; the API
  returns `uid` as `id`. Business codes are unique columns.
- Nested value objects may be typed `jsonb` (shape = the `libs/shared` type); collections you query or
  join are rows.
- Every table has `created_at / created_by / updated_at / updated_by` stamped by the API, plus soft
  delete where the model says so.
- An append-only `audit_log` (before / after JSON) is written in the same transaction as the change.
  A DB trigger blocks updates and deletes on it.

**Server logic:**
- Derived data (aggregates, counters, totals, statuses) is **server-only**: computed in the **same
  transaction** as the write that causes it, or computed on read when cheap. Never written by a client;
  rejected or ignored if sent.
- No background jobs for consistency.
- Scheduled work is `POST /internal/jobs/{name}`, guarded by an `INTERNAL_SECRET` header and called by
  the scheduler container. The same secret authenticates service-to-service calls.
- Jobs are idempotent and safe to re-run.

**Authorization per route:**
- From the permission matrix in DATA_MODEL: uid from `AuthProvider.verify`, role and status from the
  users row, scope (e.g. assigned projects) from the DB.
- 403 carries the user-facing permission message; 400 carries the Zod issues, so the client can show
  field messages.

**No realtime:**
- Every mutating route declares the cache tags it invalidates and returns them in an `X-Invalidates`
  header, on errors too.
- The api-client re-fetches the live queries tagged with them.
- An «Actualizar» (refresh) action in the shell covers changes made by other users.

**Files:**
- Uploads and downloads use presigned URLs from the API (checksum mode `WHEN_REQUIRED` for
  S3-compatible stores).
- Storage paths are decided by the API; file names are sanitized.
- Objects are private.

**Composition over inheritance:**
- No base service, base repository or base entity hierarchies.
- Shared behaviour (audit stamps, soft delete, pagination, authorization, storage) is a small injectable
  or a plain function.
- `extends` only where the framework requires it.

**Front end:**
- Data access through one repository per resource, over `libs/api-client`.
- Components never call `fetch` directly.
- No global store library; signals or local state.
- Dates travel as ISO-8601 strings.
- Time zones: each {tenant/site/project} has an IANA zone; "today" and date keys (`yyyy-MM-dd`) are
  computed in that zone on the server, never from the server's UTC clock.

## Testing and CI
- Vitest in libs, scripts and APIs.
- API tests run against a throw-away Postgres (truncated between tests) with a fake `AuthProvider`.
  The real auth adapter is tested against its emulator / local instance.
- One test per permission-matrix row and per server rule.
- Browser checks follow `docs/TEST_PLAN.md`; results go in `docs/tests/YYYY-MM-DD-NN-*.md`, with a
  defect register (D-001…) in `docs/tests/README.md`.
- CI runs:
  - lint;
  - type-check;
  - all tests;
  - builds;
  - the neutrality grep (vendor auth SDK only in auth adapters, no cloud SDKs, the S3 client only in
    `storage/`);
  - `drizzle-kit check`.

## Local environment
- `npm run db:up` (Postgres :5433 + MinIO + scheduler);
- `npm run db:migrate`;
- the auth emulator if the provider has one;
- `npm run api`;
- `npm start`;
- `npm run seed` (sample data created **through the API** so derived data is computed by the real code).

The seed refuses a non-local database. Production starts from `scripts/bootstrap` (first admin only).

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
- Before starting, read the four docs, list open questions, and propose the slice order.

## Project-specific inputs
- Apps: {LIST_OF_APPS_AND_THEIR_USERS}
- APIs and their schemas: {e.g. core-api → core, billing-api → billing}
- Auth provider: {Firebase Auth | Auth0 | Keycloak | Cognito …}; sign-in methods: {…}
- Tenancy / scoping unit: {company | project | site}; time zones: {…}
- Scheduled jobs: {name — cadence — what it does}
- External integrations: {…} (each behind an interface, adapter in its own folder)
- First provider to deploy to: {…}