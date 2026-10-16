# PLAN — Plataforma de Reclutamiento y Prenómina

> **Status:** v1 — P1–P4 resolved; E1–E7 applied; **S0 done 2026-10-16** (see §2.1); P5 open · **Written:** 2026-10-16 (kickoff)
> **Companion docs:** [SPEC.md](./SPEC.md) (D1–D32, Q4) · [DATA_MODEL.md](./DATA_MODEL.md) · [ARCHITECTURE.md](./ARCHITECTURE.md) (D33–D39) · this file continues the decision log at **D40**.
> **Rule:** the four docs are authoritative. When code needs to diverge, the doc change is proposed here (§4) or in the PR first; nothing diverges silently.

---

## 1. Findings at kickoff

Read before planning: SPEC, DATA_MODEL, ARCHITECTURE, `PROJECT_KICKOFF_PROMPT.md`, the RASS design-system readme, the prototype's `data.js`, and the golden workbook `examples/PRENOMINA SEM 38 12 HRS ECOVIA VALIDADORES.xlsx` (sheet `SEM 38`, rows 6–120 = 115 employees, total in `V121`).

What the golden workbook actually contains (checked cell by cell with formulas and cached values):

| Column | What the sheet does | Matches the spec? |
|---|---|---|
| SUELDO `K` | `=J*F` | yes |
| SEPTIMO DIA `L` | `=(1/6)*F*J` → repeating decimals (e.g. 214.285, 71.42833…) | yes, but forces a rounding decision (§3, P1) |
| PRIMA DOMINICAL `M` | `=J*0.25`, 71 rows | yes |
| HORAS INCOMPLETAS `N` | `=G*hours` (3–11 h), 13 rows | yes (D15) |
| FESTIVO PRIMERA MITAD `O` | **typed constants**: 428.5 ×34, 585.7 ×5 (2 of them on VALIDADOR rows 94, 104), 249.5 ×4, 178.6 ×3 | **no**: D14 gives 428.52 / 585.60; 249.5 and 178.6 follow no rule (P2) |
| HORAS EXTRA `P` | `=G*hours`, 8 rows | yes |
| DESCANSO LABORADO `R` | typed 500, 3 rows | yes (fixed 500); the prototype's `cantidad × SD` is wrong |
| FESTIVO LABORADO `T` | `=J*H*2`, but `H` is empty in every row → 0 | yes (no one worked the holiday) |
| DESCUENTO `U` | empty in every row | — |
| TOTAL `V` | `=SUM(K:U)`, no rounding; `V121 = 333,189.1291666…` | rounding level undefined (P1) |
| Row 30 comment | `PENSION DEL 39%`, but **no deduction** is applied: V30 = SUM(K:U) | **no**: D16 would deduct 39 % (P3) |
| Header `V2:W3` | VALIDADORES 328.57 / 27.38, SUPERVISOR 585.71 / 48.80 | rows use 428.57 / 35.71; the header is stale and is not imported |
| Bonuses | `BONO DIAS TRABAJADOS` 450 / 600 in the header; no row pays any bonus | Q4 stays open; the golden group has every `BONUS_*` concept inactive |

Totals under each rounding reading (half-up to 2 dp):

| Reading | Σ of 115 rows |
|---|---|
| Excel's unrounded sum, rounded once | 333,189.13 |
| Each line rounded once (SPEC §5.5 "line level") | 333,188.98 |
| Each line item rounded once (kickoff, DATA_MODEL §5.12 `numeric(12,2)` items, `total = Σ items`) | 333,188.95 (3 rows differ from line-level by 0.01) |
| **Chosen (D42):** exact, each row's payable truncated to cents | **333,188.81** |

Recomputing all 115 rows from their inputs with exact `decimal` reproduces every cached Excel row value within 1e-6, which confirms the column formulas above. Rows 94 and 104 are labelled VALIDADOR but carry the SUPERVISOR rates (48.80 / 585.71), which is why they get 585.7.

Environment: Node 24.21, Docker 20.10, Firebase CLI 15.27, Java 25 (emulator) are present. **Only the .NET 8 SDK is installed**; the stack pins .NET 10 LTS in `global.json`. Building needs the .NET 10 SDK installed locally (P4).

---

## 2. Slice order

Vertical slices: each one has its schema (migration), its routes with permission-matrix filters and server rules, its xUnit tests (one per matrix row and per rule), the seed additions through the API, then its screens and a browser check written up in `docs/tests/`. One commit per finished step. Slices map to SPEC §11 sprints; the two signature flows of ARCHITECTURE §4.1 are the demo gates at the end of S5 (*candidate → Apto, seen in the portal*) and S10 (*… → employee → paid*).

The two pure libraries (payroll engine, bank layouts) come right after the foundation: they carry the numbers the client will check, need no database, and answer P1–P3 against the real files before any payroll screen exists.

| # | Slice | Contents | Done when | SPEC §11 |
|---|---|---|---|---|
| **S0** | Foundation | `global.json` (.NET 10), solution with the projects from the kickoff layout; npm workspace with Angular 22 apps `admin`, `candidates`, `clients` and libs `shared`, `api-client`, `ui`; `db/docker-compose.yml` (Postgres 18 :5433, MinIO, scheduler), `db/roles.sql`; `.env.example`; `.gitignore`; EF `PlatformDbContext` (snake_case, schema `platform`), audit stamps helper, `audit_log` + `data_access_log` with append-only trigger, `users`, `user_permissions`; `IAuthProvider` + fake + Firebase adapter (emulator test); `GET /v1/me`; route authorization filter (role, active, grants, client scope, `X-Acting-For`); FluentValidation → 400 field issues, 403 messages; `X-Invalidates`; money-as-string JSON converter; Serilog; `/health`; OpenAPI → `npm run generate:api-types`; `libs/api-client` (auth header, ISO reviver, `ApiError`, cache tags, re-fetch); RASS `mat.theme()` shell in all three apps with sign-in against the emulator and «Actualizar»; `scripts/check-neutrality.sh`; GitHub Actions CI with every gate of the kickoff; `scripts/bootstrap` (first admin); `docs/TEST_PLAN.md`, `docs/tests/README.md` (defect register D-001…) | an admin signs in locally through the emulator and sees their name from `/v1/me`; CI is green | 0 |
| **S1** | Payroll engine + golden test | `Platform.Payroll.Engine` (pure, `decimal`, `AwayFromZero`): inputs (period, attendance summary, incidents, rates, group concepts), outputs (lines + items in Excel column order); the golden test reads the workbook in `examples/` and compares all 115 rows, every column and the total, per D41 | golden test green under the rules chosen for P1–P3 | (4) |
| **S2** | Bank layouts | `Platform.Bank.Layouts`: column definitions + validation rules for Banco Azteca pagos masivos, cuentas frecuentes, Banorte Alta Masiva NH, Banregio alta; ClosedXML writers; tests reproduce the headers of the four files in `examples/` and each validation rule (Banorte rules sheet, account lengths, uppercase/accents/Ñ, CURP structure, age, phone, ≤ 2 per address, email ≤ 50) | writer output opens with the same headers/cell layout as the client's files | (5) |
| **S3** | Core registry | catalogs (zones, positions_catalog, education_levels, benefits, holidays, banks seed, document types/templates), clients + contacts + sites (`uses_*` derived), people (CURP/RFC duplicate detection), person documents (presign PUT/GET, `WHEN_REQUIRED`, sanitized names, `data_access_log`, completeness view), Usuarios (invite, edit panel, grants, deactivate, resend) with Resend `IEmailSender`; admin screens: Usuarios, Clientes (both module entries), catalogs | invite a user, upload and download a document (logged) | 0 |
| **S4** | Reclutamiento | job openings (display name view, `filled` in the same transaction as the hire, days open), recruiter assignment líder/apoyo, candidates + stage machine with required reasons, stage history, interviews, rich-text notes (Tiptap + server allowlist sanitizer), opening comments model, "Convertir en empleado" stub until S7; admin screens: Vacantes, detail + Kanban, Candidatos, candidate detail, Reclutadores + detail + Asignar vacante, recruiter workspace, cover mode banner (audited `acting_for`) | demo path steps 1–3c of ARCHITECTURE §9 work against the API | 1 |
| **S5** | Candidates app + Clients app | email-link sign-in per app domain; candidate profile, documents (camera upload), Mi postulación; client portal Mis vacantes, detail with anonymization until Apto, comments thread | **gate 1:** recruiter captures → candidate uploads INE → recruiter validates, moves to Apto → client sees the name | 1–2 |
| **S6** | Reportes de reclutamiento | the six reports (views of DATA_MODEL §4.7, fixed per §4), selector screen, Excel/PDF export (ClosedXML / QuestPDF to `exports`), behind `recruiting_reports` | owner opens Rendimiento por reclutador with seed data | 2 |
| **S7** | Personal | payroll groups (IANA zone), positions with rate history, group concepts, employees (alta, convert candidate, cambio de asignación), employee status periods (exclusion constraint, attendance pre-fill), payment accounts (AES-GCM, last4, titular, principal, vigencia), card-request auto-queue, work schedules, Calendario; screens: Clientes (nómina), Personal, employee detail tabs | Alta de empleado and Cambiar estatus work; Personal lists the seed ECOVIA staff | 3 |
| **S8** | Asistencia + jobs | attendance records, mobile capture screen, unlocks (audited), `services/scheduler`, jobs `attendance-lock`, `period-roll`, `status-roll` (idempotent, zone-aware, tests with a fixed clock) | capture a day from a phone; yesterday is locked; unlock with reason | 3 |
| **S9** | Incidencias + Prenómina | incidents (retroactivo routing to the next open period), prepayroll lines recomputed by the engine in the same transaction as each attendance / incident / status / account write, period lifecycle Capturando → Revisión, live grid with payment columns, Desglose, Ajuste manual, period Excel (current layout) + PDF + receipts; seed Semana 38 through the API | the seeded Semana 38 grid equals the golden totals (D41) | 4 |
| **S10** | Archivos de pago + Tarjetas | payment batches (Azteca, cuentas frecuentes, pendientes, rejected → re-include, uploaded → paid, deposit date), freeze at Pagada (trigger), card requests → Banorte/Banregio registration batches with preview + validation, Tarjetas recibidas → primary account | **gate 2:** demo path steps 4–7 end to end; the Azteca file opens in the bank's layout | 5 |
| **S11** | Reportes de nómina | the six reports (DATA_MODEL §5.13), selector screen, exports | Costo de nómina for ECOVIA over 4 seeded weeks | 6 |
| **S12** | Notifications, MFA, go-live | bell + email notifications, jobs `document-expiry`, `notifications-digest`; TOTP MFA optional/forced; `scripts/import-excel` (DATA_MODEL §10, with a diff report against `V121`); deploy Dockerfiles, `deploy/docker-compose.same-vm.yml`, `deploy/providers/{gcp,digitalocean}.md`; `docs/HANDOFF.md` with restore drill; hardening pass (performance ≤ 2 s at 500 employees, security review) | import of the five workbooks reproduces SEM 38; a human can deploy from the docs | 6 |

The seed (`npm run seed`, refuses a non-local `DATABASE_URL`) grows with each slice: 11 clients and 6 recruiters in S3–S4, the ECOVIA group with VALIDADOR / SUPERVISOR in S7, the Semana 38 scenario from `design-reference/data.js` in S9.

---

### 2.1 Progress

| Slice | Status | Evidence |
|---|---|---|
| S0 | **done** 2026-10-16 | 41 xUnit (incl. the Firebase adapter on the emulator), 36 Vitest (libs) + 6 (apps), lint, neutrality, migrations, generated types clean; browser check [`tests/2026-10-16-01-s0-foundation.md`](tests/2026-10-16-01-s0-foundation.md) |

## 3. Open questions (block the slice named)

| # | Question | Blocks | Proposed default |
|---|---|---|---|
| ~~P1~~ | **Resolved → D42.** Rounding level — a doc–doc contradiction. SPEC §5.5: "rounding half-up at the line level, never on intermediates". Kickoff + DATA_MODEL §5.12: round once per **line item** (items are `numeric(12,2)` and `total_net = Σ items` is a DB invariant). On SEM 38 they give 333,188.98 vs 333,188.95; the Excel prints 333,189.13 because it never rounds. | S1 | **Per line item** (keeps the stored breakdown and the invariant exact; each Excel column the clerk sees is then a rounded number that sums to the total). SPEC §5.5 changes to say "once per line item". Golden test per D41. |
| ~~P2~~ | **Resolved → D43** (SPEC Q12 sent to the client). FESTIVO PRIMERA MITAD. D14 says 4 h × hourly × 3 = 428.52 / 585.60; the sheet types 428.5 / 585.7 (≈ SD bolsillo), gives 585.7 to two VALIDADORES, and 249.5 / 178.6 to seven rows with no rule. | S1 | Engine implements D14. The golden test loads column O **as per-row input** (an imported `HOLIDAY_FIRST_HALF` incident with the sheet's amount) so every other column is still checked against the rule; ask the client what 249.5 / 178.6 and the supervisor amount on VALIDADOR rows mean. |
| ~~P3~~ | **Resolved → D50.** Pensión alimenticia. Row 30 says `PENSION DEL 39%` but its total has no deduction. Either the Excel is pre-pension (the 39 % is deducted elsewhere) or D16 is not how it works today. | S1 | Golden runs with `alimony_pct = NULL` for row 30 (the Excel is pre-pension); D16 stays and has its own unit test; confirm with the client. |
| ~~P4~~ | **Resolved:** the owner installed .NET 10.0.402. **.NET 10 SDK** was not installed (only 8.0.101). The Homebrew cask (10.0.402) needs `sudo`, which the agent cannot type. | S0 | The owner runs `brew install --cask dotnet-sdk`; `global.json` pins 10.0.x. |
| **P5** | **Supervisor scope.** SPEC §5.3 / D19: a field supervisor is an Administrador with only `payroll_attendance` and "sees nothing else". `permission_module` has no grant for Reclutamiento (Vacantes, Candidatos, Reclutadores, Clientes) nor for Inicio, so under DATA_MODEL §8 that account still sees all of recruiting. Options: (a) add a `recruiting` module (default on for new Administradores, off for supervisors); (b) rule: an Administrador whose grants are only payroll ones sees no recruiting. | S4 (recruiting routes need the rule) | (a): explicit, one more checkbox in Usuarios |
| Q12 | Why do rows 65, 81, 83, 85, 87, 99, 117 of SEM 38 get 249.50 / 178.60 of festivo 1ª mitad instead of 428.52 (SPEC §10)? | nothing (known diff in the golden) | D14 for every entitled row |
| Q4 | Bonus scheme (SPEC §10) — still open. | S9 demo columns only | All `BONUS_*` concepts inactive for ECOVIA until answered (that is what SEM 38 pays). |

---

## 4. Proposed doc changes (consistency fixes with no business effect: applied in S0 unless the owner objects)

| # | Doc | Change | Why |
|---|---|---|---|
| ~~E1~~ applied | ARCHITECTURE §2 | Drop `Platform.Audit` and `Platform.Jobs` from the project list: jobs live in `Platform.Api/Jobs/`, the audit helper in `Platform.Infrastructure` (as §3 and the kickoff layout say) | §2 contradicts §3 |
| ~~E2~~ applied | SPEC §2.2, §5.9 | Permission modules written as the enum values (`recruiting_reports`, `payroll_prepayroll`, …) | SPEC uses dots, DATA_MODEL the enum |
| ~~E3~~ applied | DATA_MODEL §4.7, §5.13 | Views join on `uid` (`cl.uid = o.client_id`, D37); `open_openings_aging` drops `o.code` (removed by D30) and adds the display name | the views as written do not compile against D37 |
| ~~E4~~ applied | DATA_MODEL §5.12 | "lines of a `closed`/`paid` period" → "lines of a `paid` period" | `closed` is not a `period_status` |
| ~~E5~~ applied | DATA_MODEL §1 | `card_requests`, `employee_bank_numbers` stay in the `payroll` table list; ARCHITECTURE §3 `Platform.Bank` reads "card-request batches, payment batches, Layouts/" | module table and repository tree disagree on card requests |
| ~~E6~~ applied | DATA_MODEL §10 | "`SEM 38` header rates (row 2) are stale; rates come from the rows" | avoid importing 328.57 / 27.38 |
| ~~E7~~ applied | SPEC §5.4–5.5, §5.9, §8, D16, §10; DATA_MODEL conventions, §5.3, §5.4, §5.11–5.12, §10; ARCHITECTURE §2 engine row | Never-round + truncate (D42), pensión removed (D50), Q12 | P1–P3 |

---

## 5. Decisions (continuing the log)

| # | Decision |
|---|---|
| D40 | **Precedence for numbers and rules:** SPEC / DATA_MODEL (and their decisions) → the client's Excel files → the prototype. The prototype (`Plataforma.dc.html`, `data.js`) is the reference for screens, flows and copy only; its formulas are not a source (e.g. it computes descanso laborado as `qty × SD`, the sheet and SPEC pay a fixed 500; it counts a festivo as a worked day). |
| D41 | **Golden test definition:** the test reads the workbook from `examples/` (ClosedXML in the test project; never transcribed by hand). Inputs per row: puesto rates from the row (`G`, `I`, `J`), días (`F`), Sunday worked (`M` present), partial hours and overtime hours (the multiplier in `N` / `P`), holiday entitlement (`O` present), descanso laborado (`R`). Expected per row: every column except `O` equals the Excel cached value within 1e-6 (Excel stores doubles; the engine is exact); `O` equals D14 (4 × costo/hora × 3); the exact row total equals Σ items; the payable equals `trunc(exact, 2)`. **Known diffs** (asserted, listed in the test with the sheet's value): 34 rows 428.50 → 428.52, 5 rows 585.70 → 585.60, rows 65, 83, 99, 117 249.50 → 428.52, rows 81, 85, 87 178.60 → 428.52 (Q12). Expected period: exact 334,655.149166…, payable Σ 334,654.83 (the sheet's own inputs give 333,188.81 payable; Excel prints 333,189.13). |
| D42 | **Never round; truncate to cents only at payment** (owner, 2026-10-16). The engine keeps every item and line total exact (C# `decimal`, stored in unconstrained `numeric`); the payable `total_net = trunc(total_net_exact, 2)`, truncation not rounding (1,234.5678 → 1,234.56); the bank file rows, batch totals, `payroll_periods.total_net` and reports' payable totals are sums of those truncated amounts. Other grid/export columns display exact values truncated to 2 dp (display only, string truncation). User-typed money (rates, incidents, salaries) stays `numeric(12,2)`. Supersedes the kickoff's "AwayFromZero once per line item" and SPEC §5.5's "half-up at the line level". |
| D43 | **FESTIVO PRIMERA MITAD = D14 for every entitled row** (owner, 2026-10-16: holidays are paid triple, 4 h × costo/hora × 3). The sheet's typed 428.5 / 585.7 are the same rule short by 2 cents / over by 10; 249.50 and 178.60 are listed as known diffs and asked of the client (SPEC Q12). |
| D44 | *(merged into D50.)* |
| D45 | **Project dependencies without cycles:** module projects (`Platform.Core`, `.Recruiting`, `.Payroll`, `.Bank`) hold their entities, EF configurations, DTOs, validators, services and endpoint mappings, and use EF Core's `DbContext` through `Set<T>()`; `Platform.Infrastructure` owns `PlatformDbContext` (applies every module's configurations, owns migrations) and registers it as `DbContext`; `Platform.Api` composes everything. `Platform.Payroll.Engine` and `Platform.Bank/Layouts` reference nothing but the BCL. No base classes; shared behaviour (stamps, soft delete, pagination, authorization, storage, audit) is injected. |
| D46 | **Money on the wire:** a `System.Text.Json` converter writes `decimal` as a string with exactly the column's scale; OpenAPI declares `type: string, format: decimal`; the generated TS type is a branded `Money` string; Zod validates `^-?\d+(\.\d{1,2})?$`; the pipe formats without `Number()` (string arithmetic only for display). |
| D47 | **Period numbering:** weekly `period_number` = ISO-8601 week of the period's end date (Semana 38 = 11–17 sep 2026, end Thursday 17 sep → ISO week 38); biweekly = 1–24 by end date. Checked against SEM 38; confirm on the next client file. |
| D48 | **Tests against Postgres:** the docker Postgres on :5433 with a dedicated `platform_test` database migrated once per run and truncated between tests; a fixed `IClock` injected everywhere "today" or lock times matter. |
| D49 | **Toolchain:** .NET 10 SDK pinned in `global.json` (10.0.402, `rollForward: latestFeature`), central NuGet versions (`Directory.Packages.props`), Node 24 LTS (`.nvmrc`), one Angular 22 workspace with TS path aliases for the libs (no npm workspaces needed), `openapi-typescript` with a scoped npm override for TypeScript 6; rich text with Tiptap in `apps/admin`, server sanitizer `HtmlSanitizer` with the DATA_MODEL §4.4b allowlist. |
| D50 | **No pensión alimenticia** (owner, 2026-10-16; revises SPEC D16): not deducted, not recorded — `employees.alimony_pct`, the `ALIMONY` concept and `concept_calc.percent_of_net` are removed; the import keeps a "PENSION …" comment as plain text in `employees.notes`. The prototype's 39 % pensión and Desglose row are not built. |
| D51 | **Festivo 1ª mitad entitlement** (confirmed by the owner 2026-10-16): an employee is entitled for each date in the period that is in `holidays` and whose attendance record has status `holiday` (the day is a festivo, not worked); `holiday_worked` pays FESTIVO LABORADO instead. In SEM 38, 46 of 115 rows were entitled, so it is not automatic for everyone. The golden fixture reads entitlement from column `O` being filled. |
| D52 | **Identity interface:** `IAuthProvider` lives in `Platform.Core/Access` (module services need it for invitations) and returns `VerifiedIdentity(uid, email, emailVerified)` — the candidate rule of DATA_MODEL §8 needs the verified email — plus `CreateUserAsync` and `CreatePasswordSetupLinkAsync`. The only implementation that touches the vendor is `Platform.Api/Auth/FirebaseAuthProvider.cs`; tests use `FakeAuthProvider`; the adapter is tested against the Auth emulator. |
| D53 | **Log tables** (`audit_log`, `data_access_log`) carry `id`, `uid`, `occurred_at`, `user_id`, `acting_for_user_id` instead of the four stamp columns (they are never updated). Monthly partitions 2026-10 … 2027-12 plus a DEFAULT partition, created by the migration (raw SQL); more months are added by a later migration. Append-only trigger for every role; the API role has INSERT/SELECT only. |
| D54 | **Local S3 = `bitnamilegacy/minio:2025.7.23`.** `minio/minio` images are no longer published (Docker Hub and Quay refuse the pull); the frozen Bitnami build is the same server, local only, buckets created from `MINIO_DEFAULT_BUCKETS`. Production uses the provider's S3-compatible bucket. |
| D55 | **Wire types:** OpenAPI `format: date-time` → `Date` (revived by `libs/api-client`), `format: date` → branded `DateKey` string (never a `Date`), `format: decimal` → branded `Decimal` string. All C# `decimal` values travel as strings (`DecimalStringJsonConverter`); JSON numbers are rejected for them. Enums travel as snake_case strings, matching the Postgres enum labels. |
| D56 | **`uid` is created by the entity** (`Guid.CreateVersion7()`, EF `ValueGeneratedNever`) so the audit row and FK fix-up know it before the insert; the column default `gen_random_uuid()` remains for raw SQL. |
| D57 | **Runtime config per app:** each app reads `/config.json` (API URL, auth project, emulator URL) before bootstrapping, so one build deploys to any static host and environment; the deploy writes the file. |
| D58 | **Local ports:** API 5080, apps 4300/4301/4302, Auth emulator 9199 (UI 4010), Postgres `PLATFORM_DB_PORT` (default 5433), MinIO `PLATFORM_S3_PORT` (default 9000). Moved off the framework defaults because other local projects use them; the repo-root `.env` overrides the database/S3 host ports. |
| D59 | **Database roles** (`db/roles.sql`): `platform_owner` owns schema `platform` and runs migrations; `platform_api` is what the API connects as (DML via default privileges, no TRUNCATE, logs insert/select only); `platform_readonly` for other services. Tests connect as `platform_api` like production and truncate as the owner. |
| D60 | **Bootstrap and seed:** `POST /internal/bootstrap` (INTERNAL_SECRET) creates the first Administrador with every grant and returns a password-setup link, and refuses once an administrator exists; `scripts/bootstrap.mjs` calls it in production. The local seed signs the owner up in the emulator (`edgar@local.test` / `plataforma-local`) and calls the same endpoint; it refuses a non-local `DATABASE_URL`. |
| D61 | **Cover mode transport:** `X-Acting-For` is accepted on any `/v1` route for an Administrador covering an *active* Recruiter (else 403); it sets the recruiter scope (`RequestContext.ScopeUserId`) and `acting_for_user_id` on every audit row. The admin app keeps the covered recruiter in a signal and the api-client sends the header. |
| D62 | **Bundles:** routes lazy-load every screen and import `libs/ui` per file (`@platform/ui/<file>`), so each app's initial bundle stays within Angular's default budget (≈ 420 kB raw, ≈ 115 kB transferred, most of it the Auth SDK). |

---

## 6. Docs to be written along the way

- `docs/TEST_PLAN.md` (S0) — browser checks per slice; results in `docs/tests/YYYY-MM-DD-NN-*.md`.
- `docs/tests/README.md` (S0) — index of runs and the defect register (D-001…).
- `docs/HANDOFF.md` (S12) — operations, env vars, backups and restore drill, rotation.
- `deploy/providers/gcp.md`, `deploy/providers/digitalocean.md` (S12).
