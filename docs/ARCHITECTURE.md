# ARCHITECTURE — Plataforma de Reclutamiento y Prenómina

> **Style:** portable web platform — static SPAs, one stateless API in a container, one PostgreSQL, S3-compatible object storage. Provider-neutral by rule (see `PROJECT_KICKOFF_PROMPT.md`): it runs the same on Google Cloud, DigitalOcean, Linode, AWS or a single VM with Docker Compose.
> **First provider:** Google Cloud (the client's brief) — `deploy/providers/gcp.md`. **Cheapest:** the same-VM `docker-compose` variant.
> **Companion docs:** [SPEC.md](./SPEC.md) · [DATA_MODEL.md](./DATA_MODEL.md) · `PLAN.md` (written at kickoff) · UI reference in `design-reference/` (Claude Design export, RASS Angular Material design system)
> **Section 8** holds the Claude Design prompt for the architecture diagram; **section 9** the consolidated prompt for the interactive web-app prototype, with iteration prompts in 9.x kept as a change log.

---

## 1. Summary

```
  Administrador / Reclutador ──▶ apps/admin       ─┐
  Candidato                  ──▶ apps/candidates   ├─ static files (any static host / CDN)
  Cliente                    ──▶ apps/clients     ─┘
                                        │  HTTPS · Bearer <provider token> · X-Invalidates on every write
                                        ▼
                      services/platform-api  (ASP.NET Core 10 (.NET 10 LTS) · one container · stateless)
                        AuthProvider.verify(token) → uid → users row → role, grants, scope
                        modules: core · recruiting · payroll · bank · audit · jobs
                        │                    │                           ▲
                        ▼                    ▼                           │ POST /internal/jobs/{name}
              PostgreSQL 18            S3-compatible storage      services/scheduler
              schema `platform`        (documents, exports)       (alpine + crontab + curl)
              EF Core migrations       presigned URLs only        INTERNAL_SECRET header
```

Sized for the client: ~10 internal users, ~150 employees, ~500 candidates a year, one solo consultant operating it. No queues, no vendor SDKs, no realtime; consistency lives in the database transaction of the write that causes it.

---

## 2. Components and why

| Component | What it is | Why |
|---|---|---|
| **apps/admin** · **apps/candidates** · **apps/clients** | Three Angular 22 SPAs (standalone components + signals, Angular Material 3 with the RASS `azure-blue` theme from `design-reference/_ds`, Firebase JS SDK only inside `apps/*/auth/firebase-*.ts`). Built to static files. | Three audiences, three sign-in flows, three deploy targets; shared UI in `libs/ui`. Subdomain per app keeps email-link sign-in scoped per audience |
| **services/platform-api** | One ASP.NET Core 10 (.NET 10 LTS) API (minimal APIs, `decimal` end-to-end for money), FluentValidation, EF Core 10 + migrations (Npgsql), Serilog JSON logs. Projects mirror DATA_MODEL: `Platform.Core`, `Platform.Recruiting`, `Platform.Payroll`, `Platform.Payroll.Engine`, `Platform.Bank`, plus `Platform.Api` (host, `Auth/`, `Jobs/`) and `Platform.Infrastructure` (EF, storage, email, crypto, audit helper) — see §3. Owns Postgres schema `platform` and its migrations. | **Kept from the brief (D33): .NET's `decimal` is the reason** — payroll and bank amounts never touch a binary float. One codebase, one deploy, one schema; the project split already matches a future `recruiting-api` / `payroll-api` if ever needed |
| **libs/shared** | TypeScript wire types generated from the API's OpenAPI document (`openapi-typescript`), plus Zod schemas for client-side form validation and money-as-string helpers | The API's C# DTOs are the single source of shapes; the TS side is generated in CI, never hand-written |
| **libs/api-client** | fetch wrapper: auth header from the client `AuthProvider`, ISO-date reviver, `ApiError` (400 → field issues, 403 → message), cache tags, `X-Invalidates` handling, presigned upload client | Components never call `fetch` |
| **Platform.Payroll.Engine** (C#) | Pure pre-payroll engine: period + attendance + incidents + rates → lines and items, exact `decimal`, never rounded; the payable net is truncated to cents (PLAN D42); no I/O. Golden test reproduces `SEM 38` row by row (PLAN D41) | The one piece of logic the client will argue about; must be testable without a database, and must use `decimal` |
| **Platform.Bank.Layouts** (C#) | Column definitions and validation rules of the Banorte alta, Banregio alta, Banco Azteca pagos masivos and cuentas frecuentes layouts (pure); rendered with ClosedXML | Layouts change by editing a definition with a test, not the endpoints |
| **Platform.Infrastructure/Crypto** | AES-256-GCM (`System.Security.Cryptography.AesGcm`) for account/card numbers, key from `ENCRYPTION_KEY` with a key-id prefix for rotation | Bank data encrypted at rest without a vendor KMS |
| **services/scheduler** | `alpine` + `crontab` + `curl` → `POST /internal/jobs/{name}` with the `INTERNAL_SECRET` header | Cron is cron everywhere; jobs are idempotent and safe to re-run |
| **PostgreSQL 18** | One database, schema `platform`, `bigint` identity + `uuid uid` per table, append-only `audit_log`, roles in `db/roles.sql` | Relational integrity, audit trail, permissions — the shape of this domain |
| **S3-compatible storage** | Bucket `documents` (person documents, private) and `exports` (generated Excel/PDF/bank files, 90-day lifecycle); AWS SDK v3 with checksum mode `WHEN_REQUIRED`; MinIO locally, GCS S3-interop / Spaces / S3 in production | Presigned PUT/GET from the API; the API never streams files |
| **Auth provider: Firebase Auth** | Email/password (+ optional TOTP MFA) for the admin app, email-link for candidates and clients. Server side: `IAuthProvider.VerifyAsync(token) → uid` with the `FirebaseAdmin` package only in `Platform.Api/Auth/Firebase*.cs`; client side: sign-in / token / sign-out behind the same interface, Firebase JS SDK only in `apps/*/src/auth/firebase-*.ts`. | Zero-ops identity; swappable (Keycloak, Auth0) by replacing one adapter. **Roles and grants are rows in `users` / `user_permissions`, never token claims**; the client learns them from `GET /v1/me` |
| **Email: Resend** | Invitations, document reminders, period-ready notices, behind `IEmailSender` with the adapter in `Platform.Infrastructure/Email/Resend*.cs` (plain HTTP, no vendor SDK) | Free tier covers the volume; swappable |

Deliberately absent: Kubernetes, queues, Cloud Functions, managed secret stores, cloud schedulers, DB connectors, custom claims, WebSockets, PostgreSQL RLS (authorization is per route, from the permission matrix).

---

## 3. Repository

```
edgar-rodriguez-recruiting-platform/
├── apps/
│   ├── admin/              Angular 22 — Reclutamiento, Nómina, Usuarios
│   ├── candidates/         Angular 22 — perfil + documentos (mobile-first)
│   └── clients/            Angular 22 — portal de vacantes (mobile-first)
├── services/
│   ├── platform-api/       ASP.NET Core 10 (.NET 10 LTS) · EF Core 10 + migrations · FluentValidation · Serilog
│   │   ├── src/Platform.Api/             host, minimal APIs, Auth/ (IAuthProvider + Firebase adapter), Jobs/
│   │   ├── src/Platform.Core/            people, documents, clients, users, catalogs
│   │   ├── src/Platform.Recruiting/      openings, candidates, notes, reports
│   │   ├── src/Platform.Payroll/         groups, employees, attendance, incidents, periods
│   │   ├── src/Platform.Payroll.Engine/  pure engine (decimal), golden tests
│   │   ├── src/Platform.Bank/            registration batches (from card requests), payment batches, Layouts/ (pure) + ClosedXML writers
│   │   ├── src/Platform.Infrastructure/  DbContext + migrations, Storage/ (S3), Email/, Crypto/, audit helper
│   │   └── tests/                        xUnit: engine golden test, layouts, one test per permission row / server rule
│   └── scheduler/          alpine + crontab + curl
├── libs/
│   ├── shared/             TS wire types generated from the API's OpenAPI + Zod schemas for client forms
│   ├── api-client/         fetch wrapper, cache tags, ApiError, upload client
│   └── ui/                 shared Angular components (tables, document checklist, money/date pipes es-MX)
├── db/                     docker-compose.yml (Postgres 18 :5433, MinIO, scheduler), roles.sql
├── deploy/                 Dockerfiles, docker-compose.same-vm.yml, providers/{gcp,digitalocean}.md
├── scripts/                seed (through the API), bootstrap (first admin), import-excel (one-off, SPEC §10 of DATA_MODEL)
├── docs/                   SPEC.md, DATA_MODEL.md, ARCHITECTURE.md, PLAN.md, HANDOFF.md, TEST_PLAN.md, tests/
├── design-reference/       Claude Design export + RASS Angular Material design system (read-only reference)
└── examples/               the client's Excel files (source of the golden test and the import)
```

---

## 4. API shape

- REST, JSON, under `/v1`; DTOs are C# records validated with FluentValidation and published as OpenAPI; `libs/shared` is generated from it. Dates travel as ISO-8601 strings; money as decimal strings (`decimal` on the server, string on the wire so no client ever parses it to a float).
- Authentication: `Authorization: Bearer <token>` → `AuthProvider.verify` → `uid` → `users` row (`is_active`, `role`) → grants and scope from the DB. Cover mode: `X-Acting-For: <recruiter uid>`, accepted only for `administrator`, stamped as `acting_for_user_id` (DATA_MODEL §7, §8).
- Authorization: one endpoint filter per route built from the permission matrix (DATA_MODEL §8); 403 carries the user-facing message, 400 the validation issues (field → message), so the client shows field errors.
- Derived data (prepayroll lines, period status, employee status, opening `filled`, document completeness, batch totals) is computed **in the same transaction** as the write that causes it, or on read when cheap; clients cannot write it.
- No realtime: every mutating route declares the cache tags it invalidates and returns them in `X-Invalidates` (also on errors); `libs/api-client` re-fetches the live queries tagged with them; the shell has an «Actualizar» action for other users' changes.
- Files: `POST /v1/files/presign-upload` → presigned PUT (key decided by the API, file name sanitized, content type and size limits) → `POST /v1/people/:id/documents` records metadata; downloads are presigned GET for 15 minutes and logged in `data_access_log`.
- Exports (period Excel/PDF, bank files) are generated by the API with ClosedXML / QuestPDF into the `exports` bucket and returned as presigned GET; bank files are the exact layouts in `Platform.Bank.Layouts`.
- Jobs: `POST /internal/jobs/{name}` guarded by the `INTERNAL_SECRET` header (also used service-to-service). Idempotent:

| Job | Cadence | What it does |
|---|---|---|
| `attendance-lock` | every 15 min | locks `attendance_records` whose group lock time (in the group's time zone) has passed; moves periods whose last day locked to `in_review` |
| `period-roll` | daily 02:00 | creates the next period per payroll group from its cut-off weekday |
| `status-roll` | daily 02:05 | returns expired vacation / sick-leave / suspension periods to `active` |
| `document-expiry` | daily 03:00 | marks expired documents, queues 30/7-day reminders |
| `notifications-digest` | daily 08:00 | sends pending email notifications through `EmailSender` |

- Time zones: each payroll group has an IANA zone; "today", lock times and `yyyy-MM-dd` date keys are computed in that zone on the server.
- Composition over inheritance: audit stamps, soft delete, pagination, authorization and storage are small injected services or static helpers; no base service/repository/entity classes, no generic repositories.

### 4.1 The two signature flows

**Candidate → employee → paid**

1. Recruiter captures a candidate (person row, invitation email through `EmailSender`).
2. Candidate completes profile and uploads documents from `apps/candidates` (presigned PUT; the API records metadata).
3. Recruiter validates documents, moves the stage to *Apto*; `apps/clients` now shows the candidate by name.
4. Client interviews, recruiter marks *Contratado*; the opening counter and `filled` status update in the same transaction; "Convertir en empleado" creates the `employees` row under the client's payroll group.
5. Supervisor captures daily attendance; `attendance-lock` freezes each day.
6. Every attendance/incident/status write recomputes that employee's prepayroll line in the same transaction; at the last-day lock the period enters *Revisión*.
7. "Generar archivos de pago" validates accounts, writes the payment batch, freezes the period as *Pagada* and returns presigned links to the Banco Azteca file (and cuentas frecuentes if needed). Card requests produce the Banorte/Banregio alta files; received cards become payment accounts.

**Attendance lock** — scheduler → `/internal/jobs/attendance-lock` → `UPDATE attendance_records SET is_locked = true WHERE …` in one statement per group zone; later edits need an `attendance_unlocks` row by an Administrador with `payroll_attendance`.

---

## 5. Environments, CI and deployment

| | local | staging | production |
|---|---|---|---|
| Postgres | `db/docker-compose.yml` :5433 | managed Postgres, smallest tier | managed Postgres (provider's smallest with backups + PITR) |
| Storage | MinIO (compose) | provider bucket (S3 API) | provider bucket, versioning on, `exports` lifecycle 90 d |
| API | `dotnet run` (`npm run api`) | one container, min 0 | one container, min 1 during business hours where the provider allows it |
| Scheduler | compose service | container | container (or the host's cron in the same-VM variant) |
| Apps | `npm start` | static host | static host + CDN, custom domains |
| Auth | Firebase emulator | Firebase project `-staging` | Firebase project `-prod` |
| Config | `.env` | env vars | env vars |

CI (GitHub Actions): lint (`dotnet format`, eslint) · type-check · all tests (xUnit for the API against a throw-away Postgres with a fake `IAuthProvider`, the Firebase adapter against the emulator; Vitest for the TS libs and apps) · builds · **neutrality grep** (`FirebaseAdmin` only in `Platform.Api/Auth/Firebase*.cs` and the Firebase JS SDK only in `apps/*/src/auth/firebase-*.ts`, no cloud SDKs anywhere, `AWSSDK.S3` only in `Platform.Infrastructure/Storage/`) · `dotnet ef migrations has-pending-model-changes` must be clean. Migrations run from the API image (`dotnet ef database update` or the migration bundle) before the new revision takes traffic.

Deployment is documented per provider in `deploy/providers/*.md`, same images and env vars everywhere:

| | Postgres | API + scheduler containers | Static apps | S3 storage |
|---|---|---|---|---|
| **GCP** (first) | Cloud SQL for PostgreSQL over TCP (public IP + authorized networks or the VPC) | Cloud Run (API, min 1 business hours) + Cloud Run service for the scheduler (min 1, no ingress) | Firebase Hosting (3 sites) | Cloud Storage through its S3-interoperability endpoint (HMAC keys) |
| **DigitalOcean** | Managed PostgreSQL | App Platform or a Droplet with Compose | App Platform static sites | Spaces |
| **Same VM** | Postgres container | Compose: api, scheduler, caddy | Caddy serves the three builds | MinIO container |

No cloud commands are run by an agent; the docs and scripts are written, a human runs them.

---

## 6. Observability, security and compliance

- Serilog JSON logs with `requestId`, `userId`, `actingForUserId`, route, duration, status; the provider's log viewer or the VM's `docker logs`. `/health` checks DB and bucket; the provider's uptime check alerts by email.
- Transport: HTTPS everywhere (provider TLS or Caddy). Identity: provider token verified per request; email-link limited to the issuing app's domain; TOTP MFA optional/forced for Administradores (Firebase requires the Identity Platform upgrade for TOTP — a note in the Firebase adapter, not an architectural dependency).
- Authorization per route from the permission matrix; roles and grants in the database; cover mode audited.
- Personal data (LFPDPPP): private buckets, presigned URLs 15 min, every view/download in `data_access_log`, aviso de privacidad at first candidate login, erasure job anonymizes `people` rows after retention.
- Bank data: account/card numbers AES-GCM encrypted in the API, last 4 digits in clear, decrypted only in the bank-file writers; exports bucket lifecycle 90 days.
- Payroll integrity: paid periods immutable (trigger), manual edits are incidents with author, attendance unlocks audited, `audit_log` append-only (trigger) and written in the same transaction.
- Secrets: env vars only (`DATABASE_URL`, `S3_*`, `ENCRYPTION_KEY`, `INTERNAL_SECRET`, `FIREBASE_*`, `RESEND_API_KEY`); rotation = redeploy with new values.
- Backups: the managed Postgres's automated backups + PITR, bucket versioning; a documented restore drill in `docs/HANDOFF.md`.

---

## 7. Cost estimate (production, monthly, USD)

| Item | GCP (first provider) | DigitalOcean | Same VM |
|---|---|---|---|
| Postgres | Cloud SQL `db-f1-micro` + 10 GB + backups ≈ 10 | Managed PG 1 GB ≈ 15 | in the VM |
| API container | Cloud Run, min 1 business hours ≈ 12 | App Platform basic ≈ 5 / Droplet shared | in the VM |
| Scheduler container | Cloud Run min 1, 128 MB ≈ 4 | ≈ 0 (same Droplet) | in the VM |
| Static apps | Firebase Hosting 0 | App Platform static 0 | in the VM |
| Storage (≈ 20 GB) | Cloud Storage ≈ 1 | Spaces 5 (250 GB incl.) | in the VM |
| Auth / email / logs | Firebase Auth 0 (≤ 50k MAU) · Resend 0 · logging 0 | same | same |
| **Total** | **≈ US$ 27 / month** | **≈ US$ 25 / month** | **≈ US$ 12–24 / month** (2 vCPU / 4 GB Droplet or e2-small) + the Firebase/Resend free tiers |

Notes: `db-f1-micro` has no SLA/HA (acceptable here; resize to `db-g1-small` ≈ 25 if the grid feels slow). Figures are mid-2026 list prices; check the provider's page before quoting.

---

## 8. Claude Design prompt — Architecture diagram

> Paste as-is. The prompt deliberately describes **only layout, groups, cards, arrows and text**; visual style comes from the existing diagram design system.

```
Create an architecture diagram following my existing diagram design system (same card style, group containers, pill edge labels, legend treatment and footer chips as my previous diagrams). Do not define colors, backgrounds or typography — only structure and content. Labels in Spanish.

TITLE (top-left):
"Plataforma de Reclutamiento y Prenómina — Edgar Rodríguez"
Subtitle: "Arquitectura en la nube · Google Cloud Platform"

CANVAS: landscape, 16:9. Flow reads left → right.

ZONE A — "Usuarios" (left column, outside the GCP boundary). Four person cards stacked vertically:
  1. "Administrador" — chip: "Admin app · MFA opcional"
  2. "Reclutador" — chip: "Admin app · solo sus vacantes"
  3. "Candidato" — chip: "App de candidatos · enlace por correo"
  4. "Cliente" — chip: "Portal de clientes · enlace por correo"

LARGE BOUNDARY BOX labeled "Proyecto GCP del cliente — us-south1 (Dallas)" containing zones B–F.

ZONE B — "Frontend" (inside boundary, left). One group container "Firebase Hosting (archivos estáticos)" with three app cards side by side:
  • "Admin app" — Angular 22 — sub-label "Reclutamiento · Nómina · Usuarios"
  • "App de candidatos" — Angular 22 — sub-label "Perfil y documentos"
  • "Portal de clientes" — Angular 22 — sub-label "Avance de vacantes"
  Below the group, a single card "Proveedor de identidad · Firebase Auth" — sub-label "Correo/contraseña · MFA TOTP · Enlace mágico" with a small chip "detrás de la interfaz AuthProvider".

ZONE C — "Backend" (center). One group container "Cloud Run (contenedores)" containing two cards:
  • "platform-api · ASP.NET Core 10 (.NET 10 LTS)" — five small module chips inside the card: "Core", "Reclutamiento", "Nómina", "Bancos", "Jobs"; sub-label "Roles y permisos en la base de datos"
  • Beneath it, one thin card "Motor de prenómina — decimal, auditable"
  • A smaller card "scheduler · cron en contenedor" — sub-label "curl → POST /internal/jobs/*"
  To the right of the group, a card "GitHub Actions / Artifact Registry" with sub-label "CI/CD · misma imagen en cualquier proveedor".

ZONE D — "Datos" (right of backend). Two cards stacked vertically:
  • "Cloud SQL · PostgreSQL 18" — sub-label "Conexión TCP · esquema platform · PITR 7 días · audit_log"; chips: "core", "recruiting", "payroll / bank"
  • "Cloud Storage (API S3)" — sub-label "Documentos (INE, CURP, RFC, NSS…) · Archivos bancarios"; chip "URLs prefirmadas 15 min"

ZONE E — "Tareas programadas" (bottom-center, under Backend). One card "Jobs internos (idempotentes)" with four job chips:
  "Bloqueo de asistencia (cada 15 min)", "Apertura de períodos y estatus de empleados (diario)", "Vencimiento de documentos (diario)", "Correos pendientes (diario)"

ZONE F — "Observabilidad y respaldos" (bottom-right, under Datos). Two cards:
  • "Cloud Logging · Error Reporting" — sub-label "Logs de API y SQL · alertas"
  • "Respaldos automáticos" — sub-label "Cloud SQL diario · versionado de bucket"

ZONE G — "Sistemas externos" (far right, outside the GCP boundary). Three cards stacked:
  • "Portal Banorte (BEM)" — chip "Alta masiva .xlsx"
  • "Portal Banregio" — chip "Alta masiva .xlsx"
  • "Banco Azteca" — chip "Pagos masivos .xlsx"
  Plus one card "Resend" — chip "Correos transaccionales".

ARROWS (solid = tráfico en tiempo real; dashed = programado o respaldo):
  solid: each user card → its app card in Firebase Hosting (Administrador and Reclutador both → Admin app)
  solid: the three app cards → Firebase Auth, pill label "inicio de sesión"
  solid: the three app cards → API card, pill label "HTTPS · token de Firebase"
  solid: API → Cloud SQL, pill label "EF Core · numeric · misma transacción"
  solid: API → Cloud Storage, pill label "URLs prefirmadas (S3)"
  solid: API → Resend, pill label "invitaciones · recordatorios"
  dashed: scheduler → API, pill label "INTERNAL_SECRET · /internal/jobs/*"
  dashed: Cloud SQL → Respaldos automáticos; Cloud Storage → Respaldos automáticos
  solid: API → Cloud Logging (thin); Cloud SQL → Cloud Logging (thin, "pgAudit")
  dashed, manual: Administrador → the three bank portals, pill label "sube el archivo generado"  (draw as a long dashed arrow along the top edge, labelled "manual")
  solid: GitHub Actions → Cloud Run and → Firebase Hosting, pill label "despliegue"

NUMBERED FLOW STEPS (small numbered badges on the arrows, with a numbered list in the bottom-left corner):
  1 El usuario inicia sesión con Firebase Auth (MFA opcional para administradores).
  2 La app llama a la API con el token; la API verifica el token y toma rol y permisos de la base de datos.
  3 La API lee y escribe en PostgreSQL en una sola transacción; documentos se suben directo al bucket con URL prefirmada.
  4 El contenedor de cron llama a los jobs internos: bloquea la asistencia del día y abre los períodos de prenómina.
  5 La API genera los archivos de Banorte, Banregio y Banco Azteca; el administrador los sube al portal del banco.

LEGEND (bottom-left, below the steps): "Línea continua = tráfico en tiempo real · Línea discontinua = tarea programada / respaldo / paso manual".

FOOTER CHIPS (bottom-right, three highlight chips):
  "Infra ≈ US$ 27 / mes"  ·  "3 apps · 1 API · 1 base de datos · portable a cualquier proveedor"  ·  "Datos personales cifrados y auditados"
```

---

## 9. Claude Design prompt — Functional web-app prototype

> Produces the **interactive, clickable, single-page prototype** used live in the next client meeting. Mock data in memory; navigation and state must work. UI in Spanish. Style is not specified — it should look like a clean, modern business web app; layout and behaviour are what matter. **This base prompt is the consolidated version (synced 2026-10-10): it already includes iterations 9.1–9.13 below, which are kept only as a change log for sessions that ran the earlier version.**

```
Build an interactive single-page prototype (one HTML file, working navigation and state, all data mocked in memory, no backend) of a Spanish-language web platform called "Plataforma de Reclutamiento y Prenómina". It must be fully clickable so I can demo it live: every button described below does something visible. Desktop-first at 1440px, but the "Asistencia", "App de candidatos" and "Portal de clientes" views must also work at 390px wide.

GLOBAL
- A top bar with the product name, a ROLE SWITCHER (segmented control) with four roles: "Administrador", "Reclutador", "Candidato", "Cliente". Switching role changes the whole app shell and the visible navigation instantly (no reload). A small "Reiniciar demo" button in the top bar resets the mock data.
- Left sidebar navigation (Administrador and Reclutador roles). Candidato and Cliente roles use a simple top navigation with 2–3 items.
- All money shown as "$ 1,234.56", dates as "dd/MM/yyyy", Spanish labels everywhere.
- No system IDs anywhere in the UI (no VAC-0001 / CAN-0001 codes). A vacante is always displayed as "Cliente – Puesto" (e.g. "ECOVIA – Validador"); if the same client has two open vacantes for the same puesto, append the solicitud date ("HEB – Auxiliar de limpieza · 02/10/2026"). Candidates are displayed by name.
- Side drawers/panels are the standard for create and edit forms everywhere; tables never expand rows inline.
- Mock data: 11 clients (EL CAMINO, GTS, FILM, ECOVIA, NM FIRE, LUNACO, HEB, CERRO DEL FRAILE, I CALIZA, CONSTRUCTORA HFC, JINSEONG TECH), 6 recruiters (Mónica Soto, Pricila de León, Belem González, Servando Moreno, Aarón Vázquez, Aarón Brandon), ~12 vacantes (HEB must have two open vacantes for the same puesto), ~30 candidatos, ~20 empleados of ECOVIA (puestos VALIDADOR and SUPERVISOR, turnos C/DÍA and C/NOCHE; 2 on Vacaciones with dates, 1 Incapacidad, 1 Baja; 3 with a second payment account whose titular is another person; 2 with no account; 1 with an invalid CLABE of 17 digits), payroll periods "Semana 38 · 11–17 sep 2026" (Revisión) and "Semana 39" (Capturando). Use realistic Mexican names.

ROLE: ADMINISTRADOR — sidebar sections and views
1. "Inicio" — dashboard with KPI tiles: Vacantes abiertas, Candidatos en proceso, Contratados del mes, Empleados activos, Asistencia de hoy (% capturada), Período de prenómina en curso. Two small lists: "Vacantes por cubrir" and "Documentos por vencer".

2. "Reclutamiento" (group)
   2a. "Vacantes" — table with columns: Cliente, Puesto, Zona, Solicitadas, Contratados, Prioridad, Estatus (Abierta / En proceso / Cubierta / Pausada / Cancelada), Días abierta, Reclutador líder. Filters by cliente, estatus and reclutador. Button "Nueva vacante" opens a side drawer form with the fields: Cliente, Zona, Puesto, Vacantes solicitadas, Sueldo mensual, Prestaciones (multi-select), Horario, Banco de pago (multi-select of banks), Nivel de estudios, Años de experiencia, Experiencia requerida, Sexo (Indistinto/Masculino/Femenino), Edad, Prioridad, Reclutador líder, Observaciones. Saving adds a row to the table.
       Clicking a row opens "Detalle de vacante": header with "Cliente – Puesto", status, counters, Reclutador líder and a button "Asignar reclutador" (side panel listing recruiters with their current vacantes count, checkbox, role Líder / Apoyo; assigning updates the header and the Vacantes table); tabs "Pipeline", "Candidatos", "Documentos requeridos", "Comentarios del cliente", "Actividad". The "Pipeline" tab is a KANBAN with columns Nuevo · Contactado · Entrevista · Apto · No apto · Contratado · Descartado; cards are draggable between columns and the counters update. Moving a card to "Contratado" shows a toast "HEB – Auxiliar de limpieza: 2 de 3 cubiertas" and a button "Convertir en empleado" appears on the card.
   2b. "Candidatos" — table: Nombre, Teléfono, Vacante (shown as "Cliente – Puesto"), Reclutador, Fuente, Etapa, Apto, Contratado, Días de proceso, Sueldo ofrecido. Button "Nuevo candidato" (drawer: search persona by nombre/CURP with "crear nueva persona"; Vacante as a searchable dropdown whose options read "Cliente – Puesto", open/in-process vacantes only, with a one-line summary under the selected option (Solicitadas / Contratadas, Reclutador); fuente; comentarios). Row click → "Perfil del candidato" laid out as: a header (name, vacante as "Cliente – Puesto", etapa pill, reclutador, "Convertir en empleado" when Contratado); a LEFT column with the "Datos personales" card (teléfono, correo, domicilio, CURP, RFC, fuente, fecha de captura) and, directly below it, a "Notas" card: a rich-text editor with a compact toolbar containing exactly H1, H2, H3, Bold, Italic, Numbered list, Bulleted list, Hyperlink (the link control asks for a URL in a popover; links open in a new tab), a "Guardar nota" button (disabled while empty) and a "Cancelar" link; under it the list of saved notes newest first, each with rendered rich text, author, date and time, an "Editar" link for the author or an Administrador (loads it back into the editor, button becomes "Actualizar nota", label "editada" after), and "Edgar (cubriendo a Mónica Soto)" as author when written in cover mode; mock 2 existing notes (one with an H2, a paragraph and a 3-item bulleted list; one with bold text and a LinkedIn hyperlink); saving a note also adds "Nota agregada" to Historial; a RIGHT column with a tab group "Documentos" (checklist INE, CURP, RFC, NSS, Acta de nacimiento, Comprobante de domicilio, Estado de cuenta, each with status pill Pendiente / Entregado / Validado / Rechazado and "Validar" / "Rechazar" buttons that change the pill, plus a completeness ring "5 de 7 · 71 %"), "Entrevistas", "Historial". Notes never appear in the Cliente or Candidato roles. The same layout is used in the Reclutador workspace.
   2c. "Reclutadores" — cards per recruiter with vacantes asignadas, candidatos presentados, contratados, % conversión; "Invitar reclutador" button. Clicking a card opens "Detalle del reclutador": header with name, email, phone, Activo toggle; KPI tiles (Vacantes asignadas, Candidatos presentados, Contratados, % conversión); a table "Vacantes asignadas" (Vacante as "Cliente – Puesto", Rol pill Líder/Apoyo, Fecha de asignación, Estatus, Días abierta, Candidatos; per-row actions "Hacer líder" and "Quitar" — "Quitar" on a líder opens a dialog to pick the new líder among the other assigned recruiters or "Sin líder"); a button "Asignar vacante" that opens a side panel titled "Asignar vacante a Mónica Soto" with a search box, a list of open/in-process vacantes not yet assigned to her (each row: "Cliente – Puesto", current líder or "Sin líder", días abierta, checkbox), a role selector Líder / Apoyo (default Apoyo; Líder when the vacante has no líder; an inline note "Pricila de León pasará a Apoyo" when replacing a líder) and "Cancelar" / "Asignar (n)"; on Asignar the rows appear in the table with today's date, in her "Mis vacantes", the vacante's Actividad gets "Asignada a Mónica Soto como líder", and a toast "2 vacantes asignadas" is shown; and a primary button "Ver como reclutador": the app switches to that recruiter's workspace (the same "Mis vacantes" / "Mis candidatos" views the Reclutador role sees, filtered to her) while the role switcher still says Administrador; a persistent banner at the top reads "Viendo como Mónica Soto · estás cubriendo a esta reclutadora" with a "Salir" button. In this mode the Admin can do everything the recruiter can (drag Kanban cards, "Nuevo candidato", notes); new candidates are attributed to the recruiter, and timeline entries show "Edgar (cubriendo a Mónica Soto)". "Salir" returns to Reclutadores and restores the full Admin sidebar. The Reclutador role has no such button anywhere.
   2d. "Clientes" — table of clients with vacantes abiertas, cubiertas, días promedio. Button "Nuevo cliente" opens a side drawer (Nombre corto, Razón social, RFC, Contacto principal, Correo, Celular, Zona); saving creates the client as a recruiting client with no further choices, adds the row with 0 vacantes, shows "Cliente creado", and the client becomes selectable in the "Nueva vacante" drawer. Row → detail with header counters Vacantes abiertas / Cubiertas / Contactos / Sitios and tabs "Vacantes" (table of this client's vacantes with a "Nueva vacante" button that opens the same drawer as 2a with Cliente preset and read-only; saving adds the row here, in 2a and in the candidate dropdowns), "Contactos" (with "Nuevo contacto" — Nombre, Puesto, Correo, Celular, toggle Contacto principal — and "Invitar al portal", which shows a toast), "Sitios" (with "Nuevo sitio" — Nombre, Zona, Calle, Número, Colonia, CP, Municipio, Estado). Counters update after each creation.
   2e. "Reportes" — only visible when "Reportes de reclutamiento" is on in Usuarios → Permisos delegados (default ON for the demo). Layout: left column with 6 reports (selected one highlighted); right side a filter bar — "Cliente" select (Todos + the 11 clients), "Reclutador" select (Todos + the 6 recruiters), "Rango" with preset chips Este mes / Trimestre / Año en curso (default Año en curso) plus two date inputs; report 3 hides the Rango control — and right-aligned "Exportar Excel" / "Exportar PDF" (toast). Changing any filter re-renders with the mock data. Reports: (1) "Rendimiento por reclutador": grouped table, one group per recruiter (header row: Reclutador, Vacantes asignadas, Cubiertas, % efectividad, Días promedio), rows per vacante: Vacante ("Cliente – Puesto"), Fecha de asignación, Presentados, Aptos, Contratados, % conversión, Días para cubrir; selecting a Reclutador shows only that group. (2) "Resumen de reclutamiento": tiles Vacantes registradas, Abiertas, En proceso, Cubiertas, Canceladas, Pausadas, Candidatos capturados, Aptos, Contratados, Conversión %, Días promedio de cobertura, each with a small Δ vs the previous equal range; below, a table per Cliente with the same counts. (3) "Vacantes abiertas y envejecimiento": table of open/in-process vacantes: Vacante, Cliente, Puesto, Prioridad, Solicitadas / Contratadas ("1 de 3"), Días abierta, Etapas (chips: Nuevo 4 · Contactado 2 · Entrevista 1 · Apto 0), Último movimiento, Reclutador, and a flag chip "Sin actividad 9 días" when the last movement is older than 7 days; sorted by Días abierta desc; row click opens the vacante detail. (4) "Embudo por etapa": one row per stage (Nuevo, Contactado, Entrevista, Apto, Contratado): Entraron, Pasaron a la siguiente, Perdidos, % que avanza, Días promedio en etapa; beside it a small table "Motivos de pérdida": No apto (reasons with counts), Descartado (reasons with counts). (5) "Fuentes de reclutamiento": per Fuente (Facebook, LinkedIn, WhatsApp, Referido, Bolsa de trabajo, Otro): Candidatos, Aptos, Contratados, % conversión, Días promedio a contratación; sorted by % conversión desc. (6) "Vacantes por cliente": per Cliente: Vacantes, Headcount solicitado, Contratados, Fill rate %, Canceladas, Días promedio de cobertura, Candidatos presentados; total row. Mock data must be consistent with the vacantes and candidatos in the prototype.

3. "Nómina" (group)
   3a. "Personal" — table of employees: Nombre, Cliente, Grupo de nómina, Puesto, Turno, Fecha de ingreso, Estatus (Activo / Vacaciones / Incapacidad / Suspensión / Baja — tooltip with dates for periods), Banco de pago (masked account "BBVA •••• 2922"), Documentos (completeness %), and a conditional quick-action column: "Solicitar tarjeta" shown ONLY when the employee has no payment account and no pending card request (opens a tiny popover Banorte / Banregio, then the cell shows a pill "Tarjeta solicitada · Banorte" and the employee appears in Prenómina → "Tarjetas por solicitar"); rows with an account show nothing there. No "Baja" quick action. Button "Alta de empleado" opens a drawer: Persona (search by nombre/CURP with "crear nueva persona"), three cascading selects Cliente → Grupo de nómina (filtered by client) → Puesto (filtered by group) — choosing a Puesto shows SD bolsillo, SD IMSS and Costo por hora as read-only defaults with a toggle "Ajustar tarifas para este empleado" that makes them editable —, Turno (C/DÍA, C/NOCHE), Fecha de ingreso, and a "Cuenta de pago" block with a radio: "Ya tiene cuenta" (Banco receptor, Tipo de cuenta, Número with live digit-length validation 14/16/18/20, Titular prefilled with the employee's name and editable) or "Solicitar tarjeta" (Banorte / Banregio — puts the employee in "Tarjetas por solicitar"). Saving adds the employee under that client.
       Row → "Detalle del empleado". Header: name, next to it the status pill with its dates when it is a period ("Vacaciones · del 14/10/2026 al 20/10/2026") and a small edit (pencil) icon button that opens the "Cambiar estatus" side panel with a radio of options and their fields — Vacaciones (Fecha inicio, Fecha fin, both required); Incapacidad (Fecha inicio, Fecha fin estimada, Folio IMSS opcional); Suspensión (Fecha inicio, Fecha fin, Motivo); Baja (Fecha de baja, Motivo select: Renuncia, Término de contrato, Abandono, Despido, Otro, and a confirmation dialog "Esta acción da de baja al empleado; su último período se pagará normalmente"); and, when a period is active, "Reactivar" (ends it today). Saving updates the pill, the row in the Personal table, and pre-fills the Asistencia tab cells for those dates with V / I / S, locked. Do NOT add an "Estatus" card or a status history list anywhere in the detail.
       Tabs: "Datos" (personal data; a card "Asignación" showing Cliente · Grupo de nómina · Puesto · Turno · Desde, with "Cambiar asignación" — same cascading selects + Fecha efectiva + Motivo — that updates the card and appends to a "Historial de asignaciones" list: Desde, Hasta, Cliente, Grupo, Puesto, Motivo), "Cuentas de pago" (list of the employee's accounts: Alias, Banco, Tipo de cuenta [Cuenta propia / Tarjeta de débito / CLABE], Cuenta masked, Titular, Vigente desde, a "Principal" badge on exactly one row; "Nueva cuenta" drawer: Alias, Banco receptor, Tipo de cuenta, Número de cuenta with live length validation — 14 or 20 for cuenta propia, 16 for tarjeta, 18 for CLABE —, Titular prefilled and editable, Vigente desde default today; per row "Hacer principal" with confirm "Los pagos a partir del período en curso se depositarán en esta cuenta" and "Dar de baja"; making another account principal changes Banco/Cuenta/Titular for that employee in the current unpaid period), "Documentos", "Asistencia", "Incidencias", "Pagos".
   3b. "Calendario" — month view with coloured dots per client site; clicking a day opens the "Hoy" panel listing sites (e.g. "ECOVIA — Estación Centro · turno día: 12 esperados · 10 capturados") with a button "Capturar asistencia" that navigates to 3c with that site and date preselected.
   3c. "Asistencia" — the signature mobile-friendly screen. Header: date picker (default today), client site select. A roster list: each employee row has name, puesto, turno and a segmented control [Presente · Parcial · Falta · Descanso · Festivo · Incapacidad]; selecting "Parcial" reveals an hours input. A top button "Todos presentes". Rows for past days show a lock icon and are disabled; a "Desbloquear día" button (Administrador only) opens a modal asking for a reason and then enables editing and shows an audit note "Desbloqueado por Edgar · motivo: …". A summary bar shows "Capturados 18/20".
   3d. "Incidencias" — table filtered by period: Empleado, Fecha, Concepto (Horas extra, Retroactivo, Descanso laborado, Prima dominical, Festivo laborado, Descuento, Pensión alimenticia), Cantidad, Importe, Motivo, Capturó. Button "Nueva incidencia" (drawer). Saving one updates the pre-payroll grid in 3e immediately. An incident dated inside a Pagada period appears in the NEXT period's Retroactivo column with a tooltip "corresponde a Semana 38".
   3e. "Prenómina" — the single module where the week ends in bank files. Select payroll group ("ECOVIA — Validadores 12 h") and period ("Semana 38 · 11–17 sep 2026"). A three-step status stepper: Capturando → Revisión → Pagada. There is NO "Calcular" button: the grid is always visible and recomputes instantly whenever attendance or an incident changes. Buttons by stage: Capturando → "Cerrar captura" (moves to Revisión; otherwise automatic when the last day locks); Revisión → "Generar archivos de pago" (primary), "Exportar Excel", "Exportar PDF"; Pagada → "Descargar archivos", "Generar archivo de pendientes" (enabled only if pending rows exist), "Registrar fecha de depósito".
       THE GRID mirrors the client's Excel with these columns in this order: Fecha ingreso, Empleado, Puesto, Turno, Días laborados, Costo × hra, Festivo, SD IMSS, SD bolsillo, Sueldo, Séptimo día, Prima dominical, Horas incompletas, Festivo 1ª mitad, Horas extra, Retroactivo, Descanso laborado, Tiempo extra doble, Festivo laborado, Descuento, Total neto a pagar, Comentarios — and then, visually separated at the right, the payment columns: Banco, Cuenta (masked "•••• 2922"), Tipo de cuenta (Tarjeta débito / CLABE / Cuenta Azteca), Titular (shown only when it differs from the employee's name, with a small "tercero" chip), Estado de pago (pill: Listo / Sin cuenta / Cuenta inválida / Pendiente / Pagado / Rechazado). A totals row at the bottom. No bank text inside Comentarios. Rates in the mock: VALIDADOR SD bolsillo 428.57, SD IMSS 278.80, costo/hora 35.71; SUPERVISOR 585.71 / 278.80 / 48.80. Formulas: Sueldo = SD bolsillo × días; Séptimo día = SD bolsillo × días / 6; Prima dominical = SD bolsillo × 0.25; Festivo 1ª mitad = 4 × costo/hora × 3; Festivo laborado = SD bolsillo × festivos × 2; Total = earnings − descuento; if an employee has "Pensión alimenticia 39 %", deduct 39 % of that net last and show it in the Desglose. An employee on Vacaciones or Incapacidad for the whole period shows Días laborados = 0 and a Comentarios hint "Vacaciones 14/10–20/10" (keep the $0 row). Rows with "Sin cuenta" or "Cuenta inválida" show a warning icon with a tooltip and a link "Capturar cuenta" that opens the "Nueva cuenta" form inline (Titular editable); saving turns the pill to "Listo". Clicking any amount cell (Capturando/Revisión only) opens a popover "Ajuste manual" (new amount + motivo) that updates the total and adds a row in Incidencias. A right panel "Desglose" lists each concept of the selected employee with its source ("Asistencia", "Incidencia", "Puesto"). In Pagada the grid is read-only.
       Top-right of the view, two badges that open side panels: "Tarjetas por solicitar (6)" — employees queued for a card, grouped by bank (Banorte 4, Banregio 2), with buttons "Generar alta Banorte" / "Generar alta Banregio" that open a preview of the bank's exact columns (Banorte: Número de empleado, Sueldo bruto mensual = 100,000.00, CURP, Correo, Celular, Código nacional de ocupación = 09-TECNICOS, Calle, Número, Colonia, CP, Producto = 705-Nómina Banorte 2, Tipo de tarjeta = GENÉRICA, Número de tarjeta, Sucursal, RFC; Banregio: Nómina, Nombre, ApePaterno, ApeMaterno, RFC, CURP, Calle, NumCalle, Colonia, CodPostal, TelefonoCelular, FechaNac, Genero, Puesto, CorreoElectronico, Tarjeta, Estado de nacimiento) with validation badges on rows that break a rule (e.g. "celular con dígitos repetidos", "CURP de extranjero"), then "Descargar" → toast and the batch moves to a list inside the panel with status pills Generado → Subido al banco → Tarjetas recibidas (clickable); "Tarjetas recibidas" opens a form to type each employee's 16-digit card, after which those employees leave the queue, their Estado de pago becomes "Listo" and the Banco column shows the new card. And "Pendientes de pago (2)" — rows of this period not yet paid, with their blocker and the "Generar archivo de pendientes" button.
       "Generar archivos de pago" opens a confirmation modal: summary (Empleados listos 18 · Total $ 58,934.50 · Pendientes 2 — "se quedarán en Pendientes de pago"), then a preview table of the Banco Azteca layout (Cuenta de cargo, Titular = MAXIMA REPARACION Y LIMPIEZA SA DE CV, RFC titular, Leyenda = PAGO, Importe, Moneda = MXP, Clave banco receptor, Tipo de cuenta 1/3/40, Cuenta beneficiario, Nombre beneficiario = the titular of the principal account, Leyenda abono = PAGO, Tipo de pago = 3 when the receiving bank is Azteca, 1 otherwise — derived, not editable), a note "Se generará además el archivo de cuentas frecuentes para 3 cuentas nuevas" when applicable, and "Generar". On confirm: toast "PAGO_ECOVIA_SEM38.xlsx generado", the stepper moves to Pagada, the grid freezes, paid rows show "Pendiente", and a tab "Archivos" appears under the grid listing the generated files (Pagos masivos Azteca, Cuentas frecuentes) with status Generado → Subido al banco (clickable; on "Subido al banco" rows become "Pagado") and a "Marcar rechazado" action per row that sends it to Pendientes de pago.
   3f. "Clientes (nómina)" — two stacked tables. Table 1 "Clientes con proyectos activos" (only clients with at least one payroll group with active employees): Cliente, Grupos de nómina (chips, e.g. "Validadores 12 h"), Puestos (count), Empleados activos, Próximo corte, Período en curso (e.g. "Semana 38 · Revisión"); mock: ECOVIA (2 puestos, 20 empleados), CONSTRUCTORA HFC (grupo "Cuadrilla obra", 2 puestos, 8 empleados), HEB (grupo "Limpieza oficinas", 1 puesto, 4 empleados). Table 2 "Todos los clientes" (the 11 clients): Cliente, Razón social, RFC, Grupos de nómina (count), Empleados activos, Estatus pill ("Activo" when in Table 1, otherwise "Sin proyecto"); a search box filters it. Button "Nuevo cliente" (drawer: Nombre corto, Razón social, RFC, Contacto principal, Correo, Celular — nothing else to choose) creates a payroll client, adds it to Table 2 as "Sin proyecto", shows "Cliente creado" and opens its detail on the Grupos tab with the empty state "Este cliente aún no tiene grupos de nómina". No editable rate inputs in either table. Row click → "Detalle del cliente (nómina)": header with name, razón social, RFC, counters; tabs "Grupos de nómina" (table: Nombre, Tipo de período Semanal/Quincenal, Día de corte, Horas por turno, Hora de bloqueo de asistencia, Empleados activos, Estatus; "Nuevo grupo de nómina" drawer with those fields, default Semanal, 12 h, 23:59) and "Puestos" (table: Grupo de nómina, Puesto, SD bolsillo, SD IMSS, Costo por hora, Bono días trabajados, Empleados, "Editar" per row; "Nuevo puesto" drawer: Grupo de nómina from this client's groups, Nombre del puesto, SD bolsillo, SD IMSS, Costo por hora, Bono días trabajados, money inputs formatted "$ 0.00"; mock for ECOVIA: VALIDADOR 428.57 / 278.80 / 35.71 / 450.00 and SUPERVISOR 585.71 / 278.80 / 48.80 / 600.00). New groups and positions become selectable in Personal immediately, and the Prenómina grid uses the assigned Puesto's rates.
   3g. "Reportes de nómina" — one screen: a left report selector (6 items), a filter bar (Cliente select with "Todos"; a date control that changes per report: "Semana" picker with ◀ ▶ — default "Semana 38 · 11–17 sep 2026" — for reports 1–4, with a "Rango" toggle on 1, 3 and 4 that swaps it for two date inputs; "Rango de meses" for report 5; no date control for report 6), and "Exportar Excel" / "Exportar PDF" buttons (toast). Reports: (1) "Costo de nómina por cliente": columns Semana, Cliente, Empleados pagados, Sueldo, Séptimo día, Prima dominical, Horas (incompletas + extra), Festivos, Bonos, Retroactivos, Descuentos, Pensión, Total neto, Δ $ vs semana anterior, Δ %; with Cliente = Todos one row per client plus total; in Rango mode one row per week per client plus total; row click opens that period in Prenómina. (2) "Resumen de pago": four tiles (Total generado, Subido al banco, Pendientes, Rechazados — count and amount), a table "Por banco receptor" (Banco, Tipo de pago Traspaso BAZ / SPEI, Pagos, Importe), and a table "Pendientes y rechazados" (Empleado, Importe, Motivo: Sin cuenta / Cuenta inválida / Rechazado por el banco, link "Corregir" to Prenómina). (3) "Incidencias del período": Fecha, Empleado, Concepto, Cantidad, Importe, Motivo, Capturó, Origen (pill: Asistencia / Incidencia / Ajuste manual / Retroactivo with tooltip "corresponde a Semana 37"); subtotals per Concepto and grand total. (4) "Asistencia por empleado": Empleado, Puesto, Días laborados, Faltas, Faltas automáticas, Incapacidad, Permisos, Horas incompletas, Horas extra, Festivos laborados; sortable (default Faltas desc); toggle "Ver cuadrícula" → employee × day grid for the selected week/range with cells P / Pa / F / D / H / I / V / S, empty cells for days locked without capture highlighted, and a site select above the grid. (5) "Rotación": per Cliente × Mes: Altas, Bajas, Bajas ≤ 30 días, Headcount inicio, Headcount fin, Antigüedad promedio (meses); below, "Altas y bajas del rango": Fecha, Empleado, Cliente, Movimiento (Alta / Baja), Origen (Vacante "HEB – Auxiliar de limpieza" / Directo), Motivo de baja. (6) "Tarjetas y cuentas": four tiles (Sin cuenta, Cuenta de tercero, Solicitudes de tarjeta pendientes, No registradas como frecuentes) and a table: Empleado, Cliente, Banco, Cuenta (masked), Titular, Situación, Solicitud (Banorte · Generado / Banregio · Subido al banco / —), link "Cuentas de pago" opening the employee detail on that tab. No "documentos vencidos" report anywhere. Mock data consistent with Semana 38.

4. "Usuarios" — table: Nombre, Correo, Rol (Administrador / Reclutador / Cliente), Cliente (for Cliente role), MFA, Último acceso, Activo. Rows do not expand. Button "Invitar usuario" opens a side panel (Nombre, Correo, Rol; choosing "Cliente" shows a client select; for Administrador, the "Permisos delegados" checkboxes). Clicking a row opens the SAME side panel in edit mode titled "Editar usuario": Nombre, Correo (read-only once the invitation was accepted), Rol, Cliente (Cliente role), toggles "Activo" and "Requerir MFA", a "Permisos delegados" section for Administradores with checkboxes per module (Reportes de reclutamiento, Prenómina, Asistencia, Incidencias, Personal, Archivos de pago, Solicitud de tarjetas, Usuarios) and an optional "Solo para el cliente" select, footer "Guardar" / "Cancelar", and secondary actions "Reenviar invitación" (toast) and "Desactivar usuario" (confirm; row's Activo pill changes). "Guardar" updates the row, shows "Usuario actualizado" and applies permissions immediately: turning "Reportes de reclutamiento" off for the demo admin hides the Reportes menu; turning it on restores it. Both panels share the same field layout.

ROLE: RECLUTADOR
- Sidebar: "Mis vacantes", "Mis candidatos". Same tables, Kanban, candidate detail (with the Notas card) and "Nuevo candidato" drawer as above, filtered to the recruiter "Mónica Soto" (4 vacantes); the Vacante dropdown lists only her open/in-process vacantes as "Cliente – Puesto". No Reportes, Clientes, Nómina, Usuarios, "Ver como reclutador" or "Asignar vacante" anywhere.

ROLE: CANDIDATO (mobile-first, 390px must look great)
- Top nav: "Mi perfil", "Mis documentos", "Mi postulación".
- "Mi perfil": a form with Nombre(s), Apellido paterno, Apellido materno, Fecha de nacimiento, CURP, RFC, NSS, Celular, Correo, Calle, Número, Colonia, CP, Municipio, Estado; "Guardar" shows a toast.
- "Mis documentos": checklist cards (INE, CURP, RFC, NSS, Acta de nacimiento, Comprobante de domicilio, Estado de cuenta) each with status pill and a "Subir foto o PDF" button that simulates an upload (progress → "Entregado"). A rejected one shows the reason "Imagen ilegible".
- "Mi postulación": vacante as "Cliente – Puesto", recruiter name and phone, a friendly stage tracker (Recibida → Contactado → Entrevista → Resultado) with the current step highlighted.

ROLE: CLIENTE (mobile-friendly)
- Logged in as "HEB". Top nav: "Mis vacantes", "Comentarios".
- "Mis vacantes": cards per vacante ("Cliente – Puesto") with estatus, "2 de 3 contratados", días abierta, reclutador líder, and a mini funnel (Nuevo · Contactado · Entrevista · Apto · Contratado counts). Never show the Sexo or Edad fields of the vacante here.
- Vacante detail: candidate list where candidates before "Apto" show only initials ("J. R."), fuente and etapa; candidates at Apto or later show full name, sueldo ofrecido, fecha de entrevista and a documents completeness % (never the documents, never notes). A right-side comment thread with an input; posting adds a message.
- No exports, no access to anything else.

DEMO PATH (make sure every step works end-to-end with the mock data)
1. Administrador → Vacantes → "Nueva vacante" for cliente HEB, 3 auxiliares → appears in the table and in the HEB client detail.
2. Candidatos → "Nuevo candidato" (pick "HEB – Auxiliar de limpieza" in the dropdown) → open profile → validate two documents → completeness changes → write a note with an H2 and a bulleted list → it appears at the top of the Notas list.
3. Vacante detail → Kanban → drag the candidate to Apto → switch role to Cliente (HEB) → the candidate now appears by name.
3b. Administrador → Reclutadores → Mónica Soto → "Ver como reclutador" → move one of her candidates to Entrevista → the activity entry reads "Edgar (cubriendo a Mónica Soto)" → Salir.
3c. Reclutadores → Mónica Soto → "Asignar vacante" → pick the new HEB vacante from step 1 as Líder → "Ver como reclutador" shows it in Mis vacantes.
4. Back to Administrador → drag to Contratado → "Convertir en empleado" → Nómina → Personal shows the new employee.
4b. Clientes (nómina) → open HEB → Puestos → "Nuevo puesto" AUXILIAR DE LIMPIEZA (SD bolsillo 350.00, SD IMSS 278.80, costo por hora 29.17, bono 300.00) → Personal → open the employee from step 4 → "Cambiar asignación" to HEB / Limpieza oficinas / AUXILIAR DE LIMPIEZA → Prenómina shows the new rates for that employee.
5. Asistencia → mark the roster for today → try to edit yesterday → locked → "Desbloquear día" with reason.
6. Incidencias → add "Horas extra · 3" for one employee → Prenómina → the grid row and total change by themselves (no button) → one row shows "Sin cuenta" → "Capturar cuenta", type a CLABE → pill turns "Listo".
6b. Personal → open an employee → Cuentas de pago → "Nueva cuenta" with a different Titular → "Hacer principal" → back in Prenómina the row shows the new Banco/Cuenta and the Titular column with the "tercero" chip; the Azteca preview shows that titular as Nombre beneficiario.
7. Prenómina → "Generar archivos de pago" → confirm → stepper to Pagada, file in the "Archivos" tab → "Subido al banco" → rows "Pagado". Then "Tarjetas por solicitar" → "Generar alta Banorte" → preview with one validation badge → Descargar → "Tarjetas recibidas" → type a card → that employee now shows Banorte in the Banco column.
8. Usuarios → click the demo admin → "Editar usuario" → toggle "Reportes de reclutamiento" off → Guardar → Reportes disappears from the menu; toggle back on.
9. Personal → open an employee → pencil next to the name → Vacaciones with dates → pill and Asistencia cells update.
10. Switch to Candidato → upload a document → switch back to Administrador → that document shows as "Entregado" in the candidate's profile.

Keep all state in memory so the demo flows in sequence without reloading.
```

### 9.1 Iteration 1 — Clientes (nómina) and assignment of employees (2026-10-07)

> Paste into the **same** Claude Design session that produced the prototype, so it edits the existing file. The base prompt above already includes these changes (3a, 3g) for a fresh run.

```
Modify the existing prototype. Keep everything else exactly as it is (role switcher, navigation, mock data, demo path). Only the two views below change.

1) ADMINISTRADOR → Nómina → "Clientes (nómina)"
Replace the current view with two stacked tables and a detail view.

Table 1 — "Clientes con proyectos activos" (only clients that have at least one payroll group with active employees). Columns: Cliente, Grupos de nómina (chips, e.g. "Validadores 12 h"), Puestos (count), Empleados activos, Próximo corte (date), Período en curso (e.g. "Semana 38 · Calculado"). Mock: ECOVIA (2 puestos, 20 empleados), CONSTRUCTORA HFC (1 grupo "Cuadrilla obra", 2 puestos, 8 empleados), HEB (1 grupo "Limpieza oficinas", 1 puesto, 4 empleados).

Table 2 — "Todos los clientes" (the 11 clients). Columns: Cliente, Razón social, RFC, Grupos de nómina (count), Empleados activos, Estatus pill ("Activo" when it appears in Table 1, otherwise "Sin proyecto"). A search box filters this table. Button "Nuevo cliente" opens a drawer with: Nombre corto, Razón social, RFC, Contacto principal, Correo, Celular — nothing else to choose. Saving adds the row to Table 2 with status "Sin proyecto" and shows a toast.

No editable rate inputs in either table — rates live only in the detail view.

Clicking a row in either table opens "Detalle del cliente (nómina)": header with the client name, razón social, RFC and the counters; two tabs:
  • Tab "Grupos de nómina": table with Nombre, Tipo de período (Semanal / Quincenal), Día de corte, Horas por turno, Hora de bloqueo de asistencia, Empleados activos, Estatus. Button "Nuevo grupo de nómina" opens a drawer with those fields (Día de corte is a weekday select; default Semanal, 12 h, 23:59). Saving adds the row and the group becomes selectable in the Puestos tab and in Personal.
  • Tab "Puestos": table with Grupo de nómina, Puesto, SD bolsillo, SD IMSS, Costo por hora, Bono días trabajados, Empleados, with "Editar" on each row. Button "Nuevo puesto" opens a drawer: Grupo de nómina (select from this client's groups), Nombre del puesto, SD bolsillo, SD IMSS, Costo por hora, Bono días trabajados — all money inputs formatted "$ 0.00". Mock for ECOVIA: VALIDADOR 428.57 / 278.80 / 35.71 / 450.00 and SUPERVISOR 585.71 / 278.80 / 48.80 / 600.00. Saving adds the row and makes the position selectable in Personal.
  When a client has no payroll group yet, the Grupos tab shows an empty state "Este cliente aún no tiene grupos de nómina" with the "Nuevo grupo de nómina" button.

2) ADMINISTRADOR → Nómina → "Personal"
Add client and position assignment.
  • Table: add the columns Cliente, Grupo de nómina and Puesto (keep the existing ones).
  • "Alta de empleado" drawer: Persona (search by nombre/CURP with "crear nueva persona"), then three cascading selects — Cliente → Grupo de nómina (filtered by the chosen client) → Puesto (filtered by the chosen group). Choosing a Puesto shows its SD bolsillo, SD IMSS and Costo por hora as read-only values with a toggle "Ajustar tarifas para este empleado" that turns them into editable inputs. Then Turno (C/DÍA, C/NOCHE), Fecha de ingreso, and a "Cuenta de pago" block with a radio: "Ya tiene cuenta" (Banco + Tipo + Número + Titular prefilled with the employee's name) or "Solicitar tarjeta" (Banorte / Banregio). Saving adds the employee to the table under that client; with "Solicitar tarjeta" the employee also appears in Prenómina's "Tarjetas por solicitar" panel.
  • Employee detail → tab "Datos": add a card "Asignación" showing Cliente · Grupo de nómina · Puesto · Turno · Desde (date), with a button "Cambiar asignación" that opens the same cascading selects plus Fecha efectiva and Motivo. Saving updates the card and appends a row to a small "Historial de asignaciones" list under it (Desde, Hasta, Cliente, Grupo, Puesto, Motivo).
  • The pre-payroll grid in "Prenómina" must use the assigned Puesto's rates for each employee, so creating a new position in Clientes (nómina) and assigning an employee to it changes that employee's row when "Calcular" is pressed.

Add to the demo path, after step 4: "4b. Clientes (nómina) → open HEB → Puestos → 'Nuevo puesto' AUXILIAR DE LIMPIEZA (SD bolsillo 350.00, SD IMSS 278.80, costo por hora 29.17, bono 300.00) → Personal → open the employee converted in step 4 → 'Cambiar asignación' to HEB / Limpieza oficinas / AUXILIAR DE LIMPIEZA → Prenómina shows the new rates for that employee."
```

### 9.2 Iteration 2 — Creating clients from each module (2026-10-07)

> Paste into the same Claude Design session after iteration 1. The base prompt (2d, 3g) already reflects this for a fresh run.

```
Modify the existing prototype. Keep everything else exactly as it is. Only the two "Nuevo cliente" side panels change.

1) ADMINISTRADOR → Nómina → "Clientes (nómina)" → "Nuevo cliente"
Remove the toggles "Usa reclutamiento" and "Usa nómina" from the side panel. The panel has only: Nombre corto, Razón social, RFC, Contacto principal, Correo, Celular, and the buttons "Cancelar" / "Guardar". Saving creates the client as a payroll client automatically (no checkbox, no question), adds it to Table 2 "Todos los clientes" with status "Sin proyecto", shows a toast "Cliente creado", and opens "Detalle del cliente (nómina)" on the Grupos de nómina tab with its empty state, so the next click is "Nuevo grupo de nómina".

2) ADMINISTRADOR → Reclutamiento → "Clientes" → add "Nuevo cliente"
Add a "Nuevo cliente" button at the top-right of the clients table. It opens a side panel with: Nombre corto, Razón social, RFC, Contacto principal, Correo, Celular, Zona (select of municipios de Nuevo León), and the buttons "Cancelar" / "Guardar". Saving creates the client as a recruiting client automatically (no checkbox, no question), adds a row to the table with 0 vacantes, shows a toast "Cliente creado", and the client becomes selectable immediately in the "Nueva vacante" drawer's Cliente select.

Both panels use the same fields and layout. A client created in one module can later be used in the other without any extra step: if a recruiting client gets its first payroll group in Clientes (nómina) it simply starts appearing there, and vice versa. Do not add any module selector, checkbox or "tipo de cliente" field anywhere.
```

### 9.3 Iteration 3 — Prenómina absorbs the bank files (2026-10-07)

> Paste into the same Claude Design session after iteration 2. The base prompt (3a, 3e, 3f, demo path) already reflects this for a fresh run.

```
Modify the existing prototype. Keep everything else exactly as it is. This iteration removes the "Archivos bancarios" menu item and makes "Prenómina" the single place where a period ends in bank files.

1) Nómina → remove the sidebar item "Archivos bancarios" and its view.

2) Nómina → "Prenómina" — replace the 5-step stepper with 3 steps: Capturando → Revisión → Pagada. Remove the "Calcular", "Enviar a revisión", "Cerrar período" and "Marcar como pagado" buttons. The grid is always visible and recalculates instantly when attendance or an incident changes (demonstrate: adding an incident in Incidencias changes the row and the totals without pressing anything). Buttons by stage: Capturando → "Cerrar captura"; Revisión → "Generar archivos de pago" (primary), "Exportar Excel", "Exportar PDF"; Pagada → "Descargar archivos", "Generar archivo de pendientes" (only if pending rows exist), "Registrar fecha de depósito".

3) Grid: keep every existing column in its order and append, visually separated at the right, four payment columns: Banco, Cuenta (masked "•••• 2922"), Tipo de cuenta (Tarjeta débito / CLABE / Cuenta Azteca), Estado de pago (pill: Listo / Sin cuenta / Cuenta inválida / Pendiente / Pagado / Rechazado). Remove any bank text from the Comentarios column of the mock data. Give 2 employees "Sin cuenta" and 1 "Cuenta inválida" (CLABE with 17 digits); those rows show a warning icon with tooltip and a link "Capturar cuenta" that opens an inline form (Banco, Tipo de cuenta, Número with live length validation 14/16/18/20); saving turns the pill to "Listo". The "Ajuste manual" popover works only in Capturando/Revisión; in Pagada the grid is read-only.

4) Add a "Titular" column next to "Cuenta", shown only when the account holder is not the employee (with a small "tercero" chip). Give 3 mock employees an account whose titular is another person.

5) Top-right of the view, two badges opening side panels:
   • "Tarjetas por solicitar (6)": employees queued for a card, grouped by bank (Banorte 4, Banregio 2). Buttons "Generar alta Banorte" / "Generar alta Banregio" open a preview with the bank's exact columns (Banorte: Número de empleado, Sueldo bruto mensual = 100,000.00, CURP, Correo, Celular, Código nacional de ocupación = 09-TECNICOS, Calle, Número, Colonia, CP, Producto = 705-Nómina Banorte 2, Tipo de tarjeta = GENÉRICA, Número de tarjeta, Sucursal, RFC. Banregio: Nómina, Nombre, ApePaterno, ApeMaterno, RFC, CURP, Calle, NumCalle, Colonia, CodPostal, TelefonoCelular, FechaNac, Genero, Puesto, CorreoElectronico, Tarjeta, Estado de nacimiento) with validation badges on rows that break a rule ("celular con dígitos repetidos", "CURP de extranjero"). "Descargar" → toast, and the batch appears in a list inside the panel with status pills Generado → Subido al banco → Tarjetas recibidas (clickable). Clicking "Tarjetas recibidas" opens a small form to type each employee's 16-digit card; on save those employees leave the queue, their Banco column shows the new card and their Estado de pago becomes "Listo".
   • "Pendientes de pago (2)": the rows of this period that cannot be paid yet, with the blocker and the button "Generar archivo de pendientes".

6) "Generar archivos de pago" opens a confirmation modal with: a summary (Empleados listos 18 · Total $ 58,934.50 · Pendientes 2 — "se quedarán en Pendientes de pago"); a preview table in the Banco Azteca layout (Cuenta de cargo, Titular = MAXIMA REPARACION Y LIMPIEZA SA DE CV, RFC titular, Leyenda = PAGO, Importe, Moneda = MXP, Clave banco receptor, Tipo de cuenta 1/3/40, Cuenta beneficiario, Nombre beneficiario = titular of the principal account, Leyenda abono = PAGO, Tipo de pago = 3 when the receiving bank is Azteca, 1 otherwise — derived, not editable); a note "Se generará además el archivo de cuentas frecuentes para 3 cuentas nuevas"; and the button "Generar". On confirm: toast "PAGO_ECOVIA_SEM38.xlsx generado", stepper moves to Pagada, grid becomes read-only, paid rows show "Pendiente", and a tab "Archivos" appears under the grid listing the generated files (Pagos masivos Azteca, Cuentas frecuentes) with status Generado → Subido al banco (clickable; on "Subido al banco" the rows turn "Pagado"). Each paid row has a "Marcar rechazado" action that sends it to Pendientes de pago.

7) Retroactivos: in Incidencias, adding an incident whose date falls inside a Pagada period shows it in the NEXT period's Retroactivo column with a tooltip "corresponde a Semana 38" (add a mock "Semana 39" period in Capturando to demonstrate).

8) Nómina → "Personal" → employee detail: add a tab "Cuentas de pago" listing the employee's accounts (Alias, Banco, Tipo de cuenta, Cuenta masked, Titular, Vigente desde, Principal badge) with "Nueva cuenta" (drawer: Alias, Banco receptor, Tipo de cuenta [Cuenta propia / Tarjeta de débito / CLABE], Número with live length validation 14/16/18/20, Titular prefilled with the employee's name and editable) and "Hacer principal" per row; making another account principal changes the Banco/Cuenta/Titular columns of that employee in the current unpaid period. In "Alta de empleado", replace the "Cuenta de pago" fields with a radio: "Ya tiene cuenta" (Banco + Tipo + Número + Titular) or "Solicitar tarjeta" (Banorte / Banregio); choosing the latter puts the employee in "Tarjetas por solicitar".

9) Usuarios → "Permisos delegados": replace the checkboxes "Alta Banorte", "Alta Banregio", "Pagos Azteca" with "Archivos de pago" and "Solicitud de tarjetas".

10) Replace demo-path steps 6 and 7 with:
   6. Incidencias → add "Horas extra · 3" → Prenómina row and total change by themselves → a row shows "Sin cuenta" → "Capturar cuenta" → type a CLABE → pill "Listo".
   7. Prenómina → "Generar archivos de pago" → confirm → Pagada, file in "Archivos" → "Subido al banco" → rows "Pagado" → "Tarjetas por solicitar" → "Generar alta Banorte" → preview with one validation badge → Descargar → "Tarjetas recibidas" → type a card → that employee now shows Banorte in Banco.
```

### 9.4 Iteration 4 — Payment accounts with titular replace "Beneficiarios externos" (2026-10-07)

> Paste into the same Claude Design session after iteration 3 (which introduced "Beneficiarios externos" and "Pagos externos"). The base prompt and §9.3 already reflect this for a fresh run.

```
Modify the existing prototype. Keep everything else exactly as it is. This iteration removes the separate "Beneficiarios externos" concept: a payee is always one of an employee's payment accounts, and an account may belong to a third party (titular).

1) Nómina → "Personal": remove the "Beneficiarios externos" tab and its drawer. Remove the mock rows UALA, FEDEX, Localiza, Eduardo Zamora.

2) Nómina → "Personal" → employee detail: add a tab "Cuentas de pago" (between "Datos" and "Documentos"). It lists the employee's accounts with columns Alias, Banco, Tipo de cuenta (Cuenta propia / Tarjeta de débito / CLABE), Cuenta (masked "•••• 2922"), Titular, Vigente desde, and a "Principal" badge on exactly one row. Buttons: "Nueva cuenta" opens a drawer with Alias, Banco receptor (select), Tipo de cuenta, Número de cuenta (live digit-length validation: 14 or 20 for cuenta propia, 16 for tarjeta, 18 for CLABE), Titular (text, prefilled with the employee's full name, editable), Vigente desde (date, default today). Per row: "Hacer principal" (confirm dialog "Los pagos a partir del período en curso se depositarán en esta cuenta") and "Dar de baja" (sets Vigente hasta). Mock: give 3 employees a second account whose Titular is a different person (e.g. "MARIA ELENA TORRES GARCIA") and make it principal for one of them.

3) Nómina → "Personal" → "Alta de empleado": in the "Cuenta de pago" block, option "Ya tiene cuenta" gets the same fields as "Nueva cuenta" (Banco, Tipo, Número, Titular prefilled and editable).

4) Nómina → "Prenómina": remove the block "Pagos externos de este período" and the "Agregar pago externo" button. Remove "Pagos externos" from the confirmation modal summary (now: Empleados listos · Total · Pendientes). Add a "Titular" column right after "Cuenta", shown only when the titular is not the employee, with a small "tercero" chip. In the Banco Azteca preview, "Nombre beneficiario" must show the titular of the principal account (so for the employee whose principal account belongs to MARIA ELENA TORRES GARCIA, that name appears, not the employee's). The inline "Capturar cuenta" form for "Sin cuenta" rows uses the "Nueva cuenta" fields, Titular included.

5) Add to the demo path, after step 6: "6b. Personal → open an employee → Cuentas de pago → Nueva cuenta with a different Titular → Hacer principal → back in Prenómina the row shows the new Banco/Cuenta and the Titular column with the 'tercero' chip; the Azteca preview shows that titular as Nombre beneficiario."
```

### 9.5 Iteration 5 — Reportes de nómina as a report selector (2026-10-07)

> Paste into the same Claude Design session after iteration 4. The base prompt (3h) already reflects this for a fresh run.

```
Modify the existing prototype. Keep everything else exactly as it is. Only Nómina → "Reportes de nómina" changes.

Replace the current view with a report-selector layout:
- Left column: a vertical list of 6 reports; the selected one is highlighted. Right side: a filter bar and the report body.
- Filter bar: "Cliente" select (Todos, ECOVIA, CONSTRUCTORA HFC, HEB, …) and a date control that CHANGES with the selected report: a "Semana" picker (default "Semana 38 · 11–17 sep 2026", with ◀ ▶ to move weeks) for reports 1–4, with a "Rango" toggle on reports 1, 3 and 4 that swaps it for two date inputs; a "Rango de meses" (two month inputs) for report 5; no date control for report 6. Right-aligned buttons "Exportar Excel" and "Exportar PDF" (toast). Changing any filter re-renders the table with the mock data.

Reports (all tables, money as "$ 1,234.56"):
1. "Costo de nómina por cliente" — columns: Semana, Cliente, Empleados pagados, Sueldo, Séptimo día, Prima dominical, Horas (incompletas + extra), Festivos, Bonos, Retroactivos, Descuentos, Pensión, Total neto, Δ $ vs semana anterior, Δ %. With Cliente = Todos, one row per client plus a total row. In Rango mode, one row per week per client plus a total. Clicking a row opens that period in Prenómina.
2. "Resumen de pago" — four tiles (Total generado, Subido al banco, Pendientes, Rechazados — count and amount), a table "Por banco receptor" with Banco, Tipo de pago (Traspaso BAZ / SPEI), Pagos, Importe, and below a table "Pendientes y rechazados" with Empleado, Importe, Motivo (Sin cuenta / Cuenta inválida / Rechazado por el banco) and a link "Corregir" to Prenómina.
3. "Incidencias del período" — table: Fecha, Empleado, Concepto, Cantidad, Importe, Motivo, Capturó, Origen (pill: Asistencia / Incidencia / Ajuste manual / Retroactivo with tooltip "corresponde a Semana 37"); group subtotals per Concepto and a grand total.
4. "Asistencia por empleado" — table: Empleado, Puesto, Días laborados, Faltas, Faltas automáticas, Incapacidad, Permisos, Horas incompletas, Horas extra, Festivos laborados; sortable by any column (default Faltas desc). A toggle "Ver cuadrícula" switches to a grid employee × day (columns = the days of the selected week/range) with cells P / Pa / F / D / H / I and empty cells for days locked without capture, highlighted; a site select appears above the grid.
5. "Rotación" — table per Cliente × Mes: Altas, Bajas, Bajas ≤ 30 días, Headcount inicio, Headcount fin, Antigüedad promedio (meses). Below, a list "Altas y bajas del rango": Fecha, Empleado, Cliente, Movimiento (Alta / Baja), Origen (Vacante "HEB – Auxiliar de limpieza" / Directo), Motivo de baja.
6. "Tarjetas y cuentas" — four tiles (Sin cuenta, Cuenta de tercero, Solicitudes de tarjeta pendientes, No registradas como frecuentes) and a table: Empleado, Cliente, Banco, Cuenta (masked), Titular, Situación (one of the four), Solicitud (Banorte · Generado / Banregio · Subido al banco / —), with a link "Cuentas de pago" that opens the employee detail on that tab.

Remove any "Documentos vencidos" report or tile from this view. Mock data must be consistent with the Prenómina period Semana 38 (same employees, same totals) so the demo can jump from report 1 to the period and back.
```

### 9.6 Iteration 6 — Reportes de reclutamiento as a report selector (2026-10-07)

> Paste into the same Claude Design session after iteration 5. The base prompt (2e) already reflects this for a fresh run.

```
Modify the existing prototype. Keep everything else exactly as it is. Only Reclutamiento → "Reportes" changes (it stays visible only when "Reportes de reclutamiento" is on in Usuarios → Permisos delegados).

Replace the current view with the same report-selector layout used in Reportes de nómina:
- Left column: 6 reports, the selected one highlighted. Right: filter bar + report body.
- Filter bar: "Cliente" select (Todos + the 11 clients), "Reclutador" select (Todos + the 6 recruiters), and "Rango" with preset chips Este mes / Trimestre / Año en curso (default Año en curso) plus two date inputs. Report 3 hides the Rango control (it is a snapshot). Right-aligned "Exportar Excel" / "Exportar PDF" (toast). Changing any filter re-renders with the mock data.

Reports:
1. "Rendimiento por reclutador" — grouped table, one group per recruiter (header row: Reclutador, Vacantes asignadas, Cubiertas, % efectividad, Días promedio), rows per vacante: Vacante ("Cliente – Puesto"), Fecha de asignación, Presentados, Aptos, Contratados, % conversión, Días para cubrir. Selecting a Reclutador in the filter shows only that group.
2. "Resumen de reclutamiento" — tiles: Vacantes registradas, Abiertas, En proceso, Cubiertas, Canceladas, Pausadas, Candidatos capturados, Aptos, Contratados, Conversión %, Días promedio de cobertura — each with a small Δ vs the previous equal range. Below: a table per Cliente with the same counts.
3. "Vacantes abiertas y envejecimiento" — table of open/in-process vacantes: Vacante, Cliente, Puesto, Prioridad, Solicitadas / Contratadas ("1 de 3"), Días abierta, Etapas (chips: Nuevo 4 · Contactado 2 · Entrevista 1 · Apto 0), Último movimiento (date), Reclutador, and a flag chip "Sin actividad 9 días" when the last movement is older than 7 days. Sorted by Días abierta desc; row click opens the vacante detail.
4. "Embudo por etapa" — a table with one row per stage (Nuevo, Contactado, Entrevista, Apto, Contratado): Entraron, Pasaron a la siguiente, Perdidos, % que avanza, Días promedio en etapa; beside it a small table "Motivos de pérdida": No apto (reasons with counts), Descartado (reasons with counts).
5. "Fuentes de reclutamiento" — table per Fuente (Facebook, LinkedIn, WhatsApp, Referido, Bolsa de trabajo, Otro): Candidatos, Aptos, Contratados, % conversión, Días promedio a contratación; sorted by % conversión desc.
6. "Vacantes por cliente" — table per Cliente: Vacantes, Headcount solicitado, Contratados, Fill rate %, Canceladas, Días promedio de cobertura, Candidatos presentados; total row.

Mock data must be consistent with the vacantes and candidatos already in the prototype (same codes, same recruiters, same clients) so clicking a row in report 3 opens a real vacante.
```

### 9.7 Iteration 7 — "Ver como reclutador" (2026-10-08)

> Paste into the same Claude Design session after iteration 6. The base prompt (2c) already reflects this for a fresh run.

```
Modify the existing prototype. Keep everything else exactly as it is. Only Reclutamiento → "Reclutadores" changes, plus a cover-mode banner.

1) Reclutamiento → "Reclutadores": clicking a recruiter card opens a "Detalle del reclutador" view: header with name, email, phone, Activo toggle; KPI tiles (Vacantes asignadas, Candidatos presentados, Contratados, % conversión); a table "Vacantes asignadas" (Vacante as "Cliente – Puesto", Rol Líder/Apoyo, Fecha de asignación, Estatus, Días abierta, Candidatos; per-row actions "Hacer líder" / "Quitar"); a button "Asignar vacante" that opens a side panel with a searchable list of open/in-process vacantes not yet assigned to this recruiter (each row: "Cliente – Puesto", líder actual, días abierta, checkbox) and a role selector Líder / Apoyo, "Asignar" adds the rows to the table and to the recruiter's "Mis vacantes" with a toast; and a primary button "Ver como reclutador". The vacante detail gets the symmetric "Asignar reclutador" button (same panel, listing recruiters).

2) "Ver como reclutador" switches the main area to that recruiter's workspace: the sidebar shows only "Mis vacantes" and "Mis candidatos" (exactly the views the Reclutador role sees, filtered to this recruiter's vacantes), while the role switcher still shows "Administrador". A persistent banner across the top of the content reads "Viendo como Mónica Soto · estás cubriendo a esta reclutadora" with a "Salir" button that returns to the Reclutadores list and restores the full Admin sidebar.

3) In this mode everything is actionable: dragging Kanban cards, "Nuevo candidato", interview scheduling, document validation, notes. A candidate created here gets Reclutador = Mónica Soto (not the Admin). Every timeline / "Actividad" entry created in this mode shows the author as "Edgar (cubriendo a Mónica Soto)".

4) The Reclutador role has no such button anywhere.

5) Add to the demo path, after step 3: "3b. Administrador → Reclutadores → Mónica Soto → 'Ver como reclutador' → move one of her candidates to Entrevista → the activity entry reads 'Edgar (cubriendo a Mónica Soto)' → Salir."
```

### 9.8 Iteration 8 — Rich-text notes in the candidate detail (2026-10-08)

> Paste into the same Claude Design session after iteration 7. The base prompt (2b) already reflects this for a fresh run.

```
Modify the existing prototype. Keep everything else exactly as it is, including the current layout of "Perfil del candidato" (header on top; left column with the "Datos personales" card; right column with the tab group Documentos / Entrevistas / Historial). This applies to the candidate detail in both the Administrador views and the Reclutador workspace (Mis candidatos).

Add a "Notas" card in the LEFT column, directly below "Datos personales", same width:
- Top of the card: a rich-text editor with a compact toolbar containing exactly these controls, in this order: H1, H2, H3, Bold, Italic, Numbered list, Bulleted list, Hyperlink. No other controls (no images, tables, colours, alignment). The hyperlink control opens a small popover asking for the URL and applies it to the selected text; links render underlined and open in a new tab. The editor area grows with content (min 4 lines). Below the editor: a "Guardar nota" button (disabled while empty) and a "Cancelar" link that clears it.
- Below the editor: the list of saved notes, newest first. Each note shows the rendered rich text, then a footer line with author name, date and time ("Mónica Soto · 08/10/2026 09:14"), an "Editar" link (visible for the note's author or an Administrador) that loads the note back into the editor with the button changed to "Actualizar nota", and the label "editada" when it was updated. Notes written in "Ver como reclutador" mode show the author as "Edgar (cubriendo a Mónica Soto)".
- Mock: two existing notes on one candidate — one with an H2 "Entrevista telefónica", a short paragraph and a 3-item bulleted list; another with bold text and a hyperlink to a LinkedIn profile.
- Saving a note adds it to the top of the list immediately and clears the editor; the Historial tab also gets an entry "Nota agregada".
- Notes never appear in the Cliente or Candidato roles.
```

### 9.9 Iteration 9 — No system codes; vacantes shown as "Cliente – Puesto" (2026-10-08)

> Paste into the same Claude Design session after iteration 8. The base prompt already reflects this for a fresh run.

```
Modify the existing prototype. Keep everything else exactly as it is. This iteration removes every system code from the UI and changes how a vacante is named.

1) Global rule: nowhere in the app may a VAC-0001 or CAN-0001 style code appear — not in tables, dropdowns, detail headers, Kanban cards, toasts, reports, the Clients app or the Candidates app. Remove the "ID" columns from the Vacantes and Candidatos tables (Administrador and Reclutador "Mis vacantes" / "Mis candidatos").

2) A vacante is always displayed as "Cliente – Puesto" (en dash with spaces), e.g. "ECOVIA – Validador", "HEB – Auxiliar de limpieza". If the same client has two open vacantes for the same puesto, append " · " and the solicitud date: "HEB – Auxiliar de limpieza · 02/10/2026". Apply this to: the Vacante column in Candidatos / Mis candidatos (replace the separate Vacante-ID column; keep Cliente and Puesto columns only if they are not redundant — in Mis candidatos keep just "Vacante"), the vacante detail header, Kanban board title, the candidate detail header, toasts ("HEB – Auxiliar de limpieza: 2 de 3 cubiertas"), the Clients app cards, the Candidates app "Mi postulación", and the reports (Rendimiento por reclutador, Vacantes abiertas y envejecimiento, Rotación's Origen column).

3) Reclutador workspace → Mis candidatos → "Nuevo candidato" side panel: the "Vacante" dropdown lists only that recruiter's open/in-process vacantes as "Cliente – Puesto", is searchable by typing, and shows under the selected option a one-line summary (Solicitadas / Contratadas, Reclutador). The Administrador "Nuevo candidato" drawer uses the same dropdown over all open vacantes.

4) Update the mock data so at least one client (HEB) has two open vacantes for the same puesto, to show the date suffix.
```

### 9.10 Iteration 10 — Assign vacantes from the recruiter detail (2026-10-08)

> Paste into the same Claude Design session after iteration 9. The base prompt (2c) already reflects this for a fresh run.

```
Modify the existing prototype. Keep everything else exactly as it is. Only Reclutamiento → "Reclutadores" → recruiter detail and the vacante detail change.

1) Recruiter detail: add a button "Asignar vacante" next to "Ver como reclutador". It opens a side panel titled "Asignar vacante a Mónica Soto" with: a search box; a list of open/in-process vacantes not yet assigned to this recruiter, each row showing "Cliente – Puesto" (e.g. "HEB – Auxiliar de limpieza"), the current líder (or "Sin líder"), días abierta, and a checkbox; a role selector "Líder" / "Apoyo" (default Apoyo; Líder when the vacante has no líder); buttons "Cancelar" / "Asignar (n)". On "Asignar": the selected vacantes appear in the recruiter's "Vacantes asignadas" table with Rol and today's Fecha de asignación, show up in that recruiter's "Mis vacantes" (Reclutador role and "Ver como reclutador"), the vacante's Actividad gets an entry "Asignada a Mónica Soto como líder", and a toast "2 vacantes asignadas" is shown. When Líder is chosen for a vacante that already has one, the panel shows an inline note "Pricila de León pasará a Apoyo".

2) Recruiter detail → "Vacantes asignadas" table: add the columns Rol (pill Líder / Apoyo) and Fecha de asignación, and per-row actions "Hacer líder" and "Quitar". "Quitar" on a líder opens a small dialog asking to pick the new líder among the other assigned recruiters (or "Sin líder" if none); confirming updates both the table and the vacante.

3) Vacante detail: add a button "Asignar reclutador" in the header that opens the same panel in reverse (list of recruiters with their current vacantes count, checkbox, Líder / Apoyo). The "Reclutador líder" shown in the header and in the Vacantes table updates accordingly.

4) Add to the demo path, after step 3b: "3c. Reclutadores → Mónica Soto → 'Asignar vacante' → pick the new HEB vacante from step 1 as Líder → 'Ver como reclutador' shows it in Mis vacantes."
```

### 9.11 Iteration 11 — Create a vacante from the client detail (2026-10-08)

> Paste into the same Claude Design session after iteration 10. The base prompt (2d) already reflects this for a fresh run.

```
Modify the existing prototype. Keep everything else exactly as it is. Only Reclutamiento → "Clientes" → client detail changes.

1) Tab "Vacantes": add a "Nueva vacante" button at the top-right of the tab. It opens the same "Nueva vacante" side drawer used in Reclutamiento → Vacantes, with the Cliente field preset to this client and read-only. Saving adds the vacante to this tab's table, to the global Vacantes table, and to the "Nuevo candidato" dropdowns (as "Cliente – Puesto"), and shows the toast "Vacante creada". Row click in this tab opens the vacante detail.

2) Tab "Contactos": add "Nuevo contacto" (drawer: Nombre, Puesto, Correo, Celular, toggle "Contacto principal") next to "Invitar al portal". Tab "Sitios": add "Nuevo sitio" (drawer: Nombre, Zona, Calle, Número, Colonia, CP, Municipio, Estado).

3) The client detail header shows the counters Vacantes abiertas / Cubiertas / Contactos / Sitios and they update after each creation.
```

### 9.12 Iteration 12 — Usuarios: edit in a side panel (2026-10-08)

> Paste into the same Claude Design session after iteration 11. The base prompt (section 4 "Usuarios") already reflects this for a fresh run.

```
Modify the existing prototype. Keep everything else exactly as it is. Only Administración → "Usuarios" changes.

1) Remove the row-expansion behaviour. Rows in the Usuarios table no longer expand; the inline "Permisos delegados" panel inside the table is removed.

2) Clicking a row opens a side panel (same component and width as "Invitar usuario") titled "Editar usuario" with: Nombre, Correo (read-only once the invitation was accepted, editable before), Rol (Administrador / Reclutador / Cliente), Cliente (select, visible only for Rol = Cliente), toggle "Activo", toggle "Requerir MFA", and — only for Rol = Administrador — a section "Permisos delegados" with the checkboxes Reportes de reclutamiento, Prenómina, Asistencia, Incidencias, Personal, Archivos de pago, Solicitud de tarjetas, Usuarios, plus an optional select "Solo para el cliente". Footer: primary "Guardar", "Cancelar". Secondary actions at the bottom of the panel: "Reenviar invitación" (toast) and "Desactivar usuario" (confirm dialog; on confirm the row's Activo pill changes and the panel closes).

3) "Guardar" updates the row in the table, shows the toast "Usuario actualizado", and applies the permissions immediately (turning "Reportes de reclutamiento" off for the demo admin hides the Reportes menu; turning it on restores it).

4) "Invitar usuario" keeps its side panel but uses the same field layout as "Editar usuario" (without the Activo/MFA toggles and the secondary actions), so both panels look identical.
```

### 9.13 Iteration 13 — Personal: quick action and dated employee status (2026-10-08)

> Paste into the same Claude Design session after iteration 12. The base prompt (3a) already reflects this for a fresh run.

```
Modify the existing prototype. Keep everything else exactly as it is. Only Nómina → "Personal" (table and employee detail) changes.

1) Personal table: remove the "Baja" quick action from every row. In its place, show a quick action "Solicitar tarjeta" ONLY on rows where the employee has no payment account and no pending card request; clicking it opens a tiny popover "Banorte / Banregio", and after choosing, the cell shows a pill "Tarjeta solicitada · Banorte" and the employee appears in Prenómina → "Tarjetas por solicitar". Rows with an account show nothing in that column. The Estatus column now uses the values Activo / Vacaciones / Incapacidad / Suspensión / Baja; for Vacaciones, Incapacidad and Suspensión a tooltip shows the dates. Mock: 2 employees on Vacaciones with dates, 1 Incapacidad, 1 Baja.

2) Employee detail header: next to the employee's name, show the current status pill and, for a period, "del 14/10/2026 al 20/10/2026", followed by a small edit (pencil) icon button. Clicking it opens a "Cambiar estatus" side panel with a radio of options and their fields:
   - Vacaciones: Fecha inicio, Fecha fin (both required).
   - Incapacidad: Fecha inicio, Fecha fin estimada (editable later), Folio IMSS (optional).
   - Suspensión: Fecha inicio, Fecha fin, Motivo.
   - Baja: Fecha de baja, Motivo (select: Renuncia, Término de contrato, Abandono, Despido, Otro), and a confirmation dialog "Esta acción da de baja al empleado; su último período se pagará normalmente".
   - When a period is active, an extra option "Reactivar" (ends the period today).
   Saving updates the pill in the header, the row in the Personal table, and pre-fills the "Asistencia" tab cells for those dates with V (vacaciones), I (incapacidad) or S (suspensión), locked. Do NOT add an "Estatus" card or a status history list anywhere in the detail.

3) In Prenómina, an employee on Vacaciones or Incapacidad for the whole period shows Días laborados = 0 and a Comentarios hint "Vacaciones 14/10–20/10" (keep the $0 row, as in the client's Excel).
```

---

## 10. Decisions added by the architecture redesign (2026-10-16)

| # | Decision |
|---|---|
| D33 | API stays **ASP.NET Core on .NET 10 LTS** (supported to Nov 2028; .NET 9 is STS and ends Nov 2026) as in the brief: `decimal` for every payroll and bank amount is non-negotiable, so the engine is a pure C# project (`Platform.Payroll.Engine`) with golden tests. The portable-platform template's Node/NestJS defaults are mapped to .NET equivalents (EF Core migrations, FluentValidation, Serilog, xUnit, OpenAPI → generated TS client) |
| D34 | Provider-neutral infrastructure: Postgres over TCP, containers, static files, S3-compatible storage, env vars; no cloud SDKs, managed queues, secret managers, cloud schedulers or DB connectors. GCP first, DigitalOcean and same-VM documented |
| D35 | One API (`platform-api`) and one Postgres schema (`platform`); DATA_MODEL modules are folders, not schemas |
| D36 | Identity via Firebase Auth behind `AuthProvider`; roles and grants in the database, never in token claims; `GET /v1/me` |
| D37 | `bigint` identity + `uuid uid` per table; FKs reference `uid`; API exposes `uid` as `id` |
| D38 | Derived data computed server-side in the same transaction; no background consistency jobs; scheduled work only through `POST /internal/jobs/*` |
| D39 | No realtime: `X-Invalidates` cache tags + «Actualizar» |

## 11. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Bank portals change their layouts | Layouts are pure definitions in `Platform.Bank.Layouts` with a version; a column change is a definition edit with a test, not an endpoint change |
| Pre-payroll rules differ per client beyond ECOVIA | Concept catalog per payroll group (DATA_MODEL §5.3); new client = configuration; golden test per group |
| Field supervisors without reliable internet | Attendance screen keeps an in-memory queue and retries; the lock rule uses `recorded_at` server-side (offline-first is explicitly out of scope for this architecture) |
| Cold starts on the API hurting attendance capture | min 1 instance in business hours where the provider allows it; the same-VM variant has no cold starts |
| Personal data breach | Private buckets, presigned URLs, encryption of bank numbers, access log, MFA for admins, env-var secrets, least-privilege DB roles |
| Provider lock-in | The neutrality grep in CI; every provider doc uses the same images and env vars |
| Solo-consultant bus factor | One monorepo, `docs/HANDOFF.md`, Compose files that run the whole thing on a laptop, Postgres backups restorable anywhere |
