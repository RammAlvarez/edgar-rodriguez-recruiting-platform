# Audit report

Tracks: duplication, tests, antipatterns, agent-dx. All findings below passed verification (none refuted). Severities are the verifier's adjusted values.

## 1. Summary

- 4 tracks ran, none failed. 31 verified findings and 12 unverified.
- Verification refuted nothing, but it lowered almost every severity. Only 5 verified findings remain at medium after adjustment (none high): scoped payroll write tests, email fallback logging, no CLAUDE.md/settings, stale D101 docs, unguarded local DB scripts.
- The product code is clean: no `any`, `@ts-ignore`, secrets, string-built SQL or sync-over-async found. Duplication is 1.19% overall (33 clones).
- Most notable risks: the email fallback logger can write password-setup links to Cloud Logging if `RESEND_API_KEY` is unset; client-scoped payroll write routes have no tests; local DB scripts (`db:migrate`, `test:api`) have no guard against remote DB URLs.
- Biggest agent-facing gaps: no CLAUDE.md/AGENTS.md, no `.claude/settings.json`, no one-command check, and stale post-D101 conversion text in docs.
- `.claude/worktrees/` is untracked and not ignored.

### Not covered

- Duplication: CSS/HTML templates, test code, unused `libs/shared` exports, Zod vs .NET validator comparison. jscpd ran with min 8 lines / 70 tokens.
- Tests: `dotnet test` was NOT run (needs Postgres on the test port; no services were to be started). .NET tests were assessed by static reading only, with no Stryker.NET and no flakiness data. Angular-dependent specs could not be mutation-tested, so auth, permissions and mfa-guard mutation scores are missing. Mutation scores were not rerun by verifiers. Pipeline and candidates backend modules and server-side permissions were not mutated.
- Antipatterns: eslint sonarjs complexity not run; largest files (PayrollReports.cs, PaymentEndpoints.cs, payment-panel.ts) not read in depth; N+1 and transaction analysis only sampled; Zod/boundary validation, rate limiting and Firebase token verification not reviewed. Terraform claims were not fully verified (grep errored).
- Agent-dx: GUIA_DE_USO beyond grep hits, full .cs/.ts sources, deploy provider guides, scheduler crontab vs `scheduler.tf`, Dockerfiles, `angular.json` reporters, `.vscode` tasks. Logging and boundary claims rest on grep counts.

## 2. Agent DX scorecard

| Area | Rating | Note |
|---|---|---|
| CLAUDE.md/AGENTS.md accuracy | missing | No CLAUDE.md or AGENTS.md; README omits the 5434 override, emulator ports and agent never-rules. |
| Decision log hygiene | needs work | Thorough, but mixed D/D- IDs, ultra-long rows, no status column; D101 supersedes D72/D82/D87 without back-links. |
| Docs drift vs code (post D101) | needs work | SPEC, GUIA_DE_USO, TEST_PLAN updated; ARCHITECTURE.md:361 and PLAN rows 58/61/83 still describe conversion. |
| Domain naming and glossary | needs work | English identifiers vs Spanish UI terms mapped only implicitly; no GLOSSARY. |
| Oversized files and barrels | good | Only seed.mjs (599), PayrollReports.cs (578) and one test file (513) exceed 500 lines; three small barrels. |
| One-command check script | missing | No `npm run check`; CI steps differ from local scripts. |
| Test speed and output noise | needs work | `test:api` needs Postgres and emulator; no terse reporters; no fast subset. |
| Scripted emulator/seed setup and port collisions | needs work | Scripted and configurable, but 5433 default, no tracked local env template, stale .vscode 4200. |
| Local vs CI parity | needs work | CI duplicates DB role setup and uses different build configs/commands. |
| Custom lint/import-boundary rules | needs work | fetch/Firebase restrictions and neutrality grep are good; no app/lib or Recruiting/Payroll boundary rules. |
| Shared schemas/types client-server | good | OpenAPI-generated `libs/shared` types with CI diff check, shared api-client, Zod. |
| Safe defaults for destructive scripts, Terraform, DEV/PROD | needs work | seed refuses non-local DB; `db:migrate`/`test:api`/import-excel have no guard; `deletion_protection` off in code and dev.tfvars. |
| .env.example completeness and secrets rules | needs work | Production vars documented; local vars not templated; `.env` ignored correctly. |
| .claude/settings.json allowlists and hooks | missing | Only `.claude/launch.json` tracked. |
| One-obvious-way recipes (endpoint/form/page/migration) | needs work | Builders and fakes exist; no recipes, skills or `db:migration:add`. |
| Test factories/fixtures | good | `Platform.Tests/Support` has ApiFactory, Builders, fakes, TestDatabase. |
| Structured logging | needs work | ILogger in 3 files; no JSON console or Serilog configured. |
| Skills and commands | missing | No project skills or slash commands. |
| .claude/worktrees gitignored | missing | Untracked and unignored; duplicates repo in greps. |

### Top 5 changes to reduce agent mistakes

1. Add a short CLAUDE.md: command table, real ports, never-rules (no terraform/gcloud/firebase deploy against DEV, no hand edits to Migrations or `libs/shared/src/generated`), post-API-change commands, D101 separation, es-MX copy, pointer to PLAN section 5.
2. Add `.claude/settings.json` with an allowlist for lint/test/typecheck/dotnet/read-only git and a deny list for terraform apply/destroy, gcloud, firebase deploy, `scripts/deploy-hosting.sh`.
3. Gitignore `.claude/worktrees/` and exclude it from eslint and `scripts/check-neutrality.sh`.
4. Add `npm run check` (and a fast variant) and make `ci.yml` call the same npm scripts.
5. Guard `db:migrate`, `test:api`, import-excel and bootstrap against non-local targets (`assertLocalDatabase` plus explicit opt-out), and fix the stale D101 text so agents stop reintroducing conversion.

## 3. Findings per track

### 3.1 Duplication (verified)

| Finding | Location | Severity | Effort | Fix |
|---|---|---|---|---|
| Identical Firebase email-link adapter in candidates and clients (note: placement follows ARCHITECTURE.md/check-neutrality.sh rule that Firebase SDK lives in per-app files) | apps/candidates/src/auth/firebase-email-link.ts and apps/clients/src/auth/firebase-email-link.ts (1-73); app.config.ts also identical | low | S | Share `app.config`, or move factory to one Firebase-only file and update the neutrality allowlist. |
| Document upload (presign, PUT, confirm) duplicated | apps/admin/src/app/data/recruiting-repositories.ts:207-225; apps/candidates/src/app/data/portal.ts:71-89 | low | S | `uploadPersonDocument(...)` helper in `libs/api-client/src/upload.ts`. |
| ApiError-to-message handling repeated (~26 sites, 5 with inline fieldErrors variant) | admin: payment-panel.ts:479, card-queue-panel.ts:326, line-panel.ts:271, mfa-page.ts:262, attendance-page.ts:465, plus ~20 simple sites | low | M | Message helper in `libs/api-client/errors.ts` (first field error, else message, else fallback). Name must not collide with `errorMessage` in `libs/ui/src/forms.ts`. |
| Same busy/error `run(work)` wrapper in seven components (error handling differs per site) | mfa-page.ts:255; card-queue-panel.ts:320; payment-panel.ts:473; prepayroll-page.ts:409; user-panel.ts:294; sign-in.ts:96; libs/ui/src/email-link-sign-in.ts:112 | low | M | Shared helper in `libs/ui` taking an `onError` callback. |
| Export audit-entry construction repeated in five endpoint files | ReportEndpoints.cs:76-92; PayrollReportEndpoints.cs:66-78; PrepayrollEndpoints.cs:198-210; PaymentEndpoints.cs:484-495; RegistrationEndpoints.cs:380-391 | low | S | `AuditEntry.ForExport(ctx, now, table, after)`. |
| Period lookup and group scoping duplicated (permission-scoped lookup) | PaymentEndpoints.cs:408-420; PrepayrollEndpoints.cs:270-282 | low | S | `PayrollScope.Period(db, ctx, module, id, ct)`. |
| Employee status-period endpoints repeat find/guard preamble | EmployeeEndpoints.cs:201-210, 231-240 | low | S | Extract `LoadPeriod(...)`. |
| Payroll period grouping computed identically in two pages | prepayroll-page.ts:319-328; incidents-page.ts:141-150 (and 335-349 vs 154-168) | low | S | Pure `groupPeriods(periods)` in `prepayroll/`. |

#### Duplication: unverified

| Finding | Location | Severity | Effort | Fix |
|---|---|---|---|---|
| Module-grant check re-implemented with three semantics | prepayroll-page.ts:315-316; libs/ui/src/session.ts:52; shell/navigation.ts:75 | low | S | One exported `hasGrant(me, module)`. |
| Side-panel/detail components share save and error-mapping blocks | client-panel/contact-panel/person-panel/candidate-panel/employee-panel/client-detail/recruiter-detail/candidate-detail | low | M | Shared `saveForm(...)` in `libs/ui/forms.ts`, after the error helper. |
| Report pages share imports and export() method | payroll-reports-page.ts:372-387; reports-page.ts:270-285 | low | M | `downloadExport(file, snack)` in `libs/ui`; keep pages separate (D101). |
| Presign/upload SQL duplicated in migration SQL helpers | PaymentFilesSql.cs:14-28; PrepayrollSql.cs:10-24 | low | S | Likely leave as is (frozen migration SQL). |

### 3.2 Tests (verified)

| Finding | Location | Severity | Effort | Fix |
|---|---|---|---|---|
| Client-scoped payroll write routes untested; scope tests are GET-only (attendance/schedule do sign in a scoped user, but incident writes are untested; scope code exists) | PrepayrollTests.cs:505-511; IncidentEndpoints.cs:81-110 | medium | M | One scoped-user negative case per mutating payroll route family, expect 404 for another client's employee or period. |
| Frontend permission guards have no tests | libs/ui/src/guards.ts:15-46; session.ts | low | S | Vitest tests with stub Session and Router for loading, denied, signed-out, wrong role, admin without grant. |
| Money formatting/comparison mutation survivors (test gaps, not bugs; Stryker not rerun) | libs/shared/src/money.ts:13-14, 28-29, 43 | low | S | Add cases: '007.5', '00' vs '0.000', '-1.5' vs '-1.50', and a case like '-5.05' for the line-14 mutant. `'-2','-1'` already exists; '-0.00' alone does not kill line 14. |
| RouteConventionTests only proves a rule exists, not which one | RouteConventionTests.cs:21-27 | low | M | Allow-list test or snapshot of route-to-rule map for `AnySignedInUser` (used only at MeEndpoints 49, 88). Several suites lack 403 tests. |
| Route-table specs restate config (guard counts only) | candidates app.routes.spec.ts:22-29; clients app.routes.spec.ts:6-12 | low | S | Replace with behavioural guard tests; keep identity checks at most. |
| api-client error mapping and cache weak coverage (generic FALLBACK for 401/404/409/500, getters, non-MFA 403 untested; 429, network failure and MFA 403 are tested) | libs/api-client/src/errors.ts; cache.ts | low | S | Add the missing status cases and cache invalidation edge cases. |
| No automated E2E layer (documented as manual in TEST_PLAN.md:3; 17 spec files, not 13) | docs/tests/2026-10-18-15-e2e.md | low | L | Optional Playwright smoke suite for three login paths. |
| navigation.ts: untested empty-section filter (line 66-69); the line-75 `&&` mutant is probably equivalent | apps/admin/src/app/shell/navigation.ts:66-75 | low | S | Test that a recruiter sees no empty section header only. |

#### Tests: unverified

| Finding | Location | Severity | Effort | Fix |
|---|---|---|---|---|
| Suite is generally substantive; only a few low-value tests (good news) | ClockTests.cs:8-13; catalog-defs.spec.ts:12-14 | low | S | None needed; optionally drop the config-restating assertion. |

### 3.3 Antipatterns (verified)

| Finding | Location | Severity | Effort | Fix |
|---|---|---|---|---|
| Email fallback `LogEmailSender` logs recipient, subject and body (including password-setup links) at Information level whenever `ResendApiKey` is empty, with no environment check | ResendEmailSender.cs:36-47; Program.cs:91-94; UserEndpoints.cs:215 | medium | S | Register only in Development; otherwise fail startup or log To/Subject only; add startup check. Terraform secret wiring (run.tf, secrets.tf) not verified. |
| Duplicated period-selection `effect()` pair with fragile skip-first flag | prepayroll-page.ts:335-350; incidents-page.ts:154-170 | low | S | Shared helper or drive the resource from `periodId` as request signal. |
| Swallowed rejection when marking notification read | apps/admin/src/app/shell/notification-bell.ts:120 | low | S | Reload list in `finally` or comment that the swallow is intentional. |
| API Cloud Run service publicly invokable (ingress ALL); `/internal/*` protected only by shared secret (documented in deploy/providers/gcp.md:77, not in PLAN section 5) | deploy/terraform/gcp/run.tf:78, 162-166; scheduler.tf:29 | low | M | Block `/internal/*` at an LB/Cloud Armor rule or use a private service; record the decision in PLAN. |
| Unused exports, dead barrel, unused/unlisted package.json entries (knip; some symbols are test-only) | navigation.ts; application-page.ts; side-panel.ts; forms.ts (`blankToNull`); api-types.ts; package.json (prettier, tsx; `firebase-tools` unlisted) | low | S | Remove or un-export; add `firebase-tools`; add `knip.json` ignoring `.claude/**` and `design-reference/**`. |

### 3.4 Agent DX (verified)

| Finding | Location | Severity | Effort | Fix |
|---|---|---|---|---|
| No CLAUDE.md/AGENTS.md and no `.claude/settings.json` | repo root; `.claude/` has only launch.json | medium | S | Add CLAUDE.md and settings allow/deny lists (see Top 5). |
| `.claude/worktrees/` not gitignored; check-neutrality.sh line 9 would scan it | .gitignore; scripts/check-neutrality.sh:9 | low | S | Ignore it; exclude in eslint and the neutrality script. |
| Stale conversion text after D101 | docs/ARCHITECTURE.md:361; docs/PLAN.md:58, 61, 83, 171, 181, 186 | medium | S | Fix ARCHITECTURE:361; mark D72/D82/D87 as superseded; strike convert wording; optional grep guard. |
| No single check command; CI and npm scripts differ (typecheck is dev config, CI builds production) | package.json:5-26; ci.yml:36-129 | low | S | `npm run check` and `check:fast`; make ci.yml call npm scripts. |
| Local DB/migration scripts have no non-local guard; `assertLocalDatabase` used only by seed.mjs. Terraform `deletion_protection` claims not verified | package.json (`db:migrate`, `test:api`); scripts/lib/env.mjs:15, 44; run.mjs; import-excel.mjs; bootstrap.mjs | medium | M | Call `assertLocalDatabase` in run.mjs with explicit opt-out; require `--yes` for remote import/bootstrap; default `db_deletion_protection=true`. |
| Port docs gap: .vscode/launch.json still uses 4200; firebase.json emulator ports undocumented (5433 is the repo default; 5434 is a local override, so not a repo defect) | .vscode/launch.json:10; firebase.json; README.md | low | S | Ports table, fix or delete stale .vscode entries, add a local env example. |
| Decision log: mixed ID formats, no status column, very long rows (up to ~2.9k chars), no back-links from superseded rows | docs/PLAN.md:137-200 | low | M | Separate DEF from D IDs, add status column, one-line rows. |
| No import-boundary lint (apps to apps, libs to apps) or .NET module-boundary test (note: Platform.Payroll already references Recruiting, so assert Recruiting does not reference Payroll) | eslint.config.js:17-50; services/** ignored | low | M | `no-restricted-imports` patterns and an architecture test. |

#### Agent DX: unverified

| Finding | Location | Severity | Effort | Fix |
|---|---|---|---|---|
| No documented recipes for endpoint/form/page/migration; no `db:migration:add` script | Support/*.cs; ARCHITECTURE.md; package.json | medium | M | Add the npm script and a CLAUDE.md "Recipes" section or skills. |
| seed.mjs is 599 lines, depends on `design-reference/data.js`, no per-slice entry points | scripts/seed.mjs; scripts/lib/sem38.mjs | low | M | Move fixtures to `scripts/lib/fixtures`, add `--only` flags. |
| Structured logging minimal (ILogger in 3 files, no JSON console) | services/platform-api/src | low | M | Enable JSON console outside Development. |
| No glossary mapping Spanish UI terms to English identifiers | docs/SPEC.md; DATA_MODEL.md; seed.mjs; PLAN.md | low | S | Add docs/GLOSSARY.md. |
| CI does not mirror local DB setup; no `infra:check`; no terse reporters | ci.yml:60-90; db/docker-compose.yml; package.json:24 | low | S | Add `infra:check`, `test:api:up`, quiet reporters. |
| .env.example is production-oriented; copying it to `.env` breaks local runs | .env.example:1-72 | low | S | Split into `.env.example` and `.env.local.example`. |

## Dismissed after verification

None. No finding had `refuted: true`. Severity was lowered on most findings; reasons are in the Notes column of each table.

## 4. Triage

### Quick wins (S effort, medium or higher after adjustment)

- Restrict `LogEmailSender` to Development or fail startup when `RESEND_API_KEY` is missing (antipatterns, medium, S).
- Add CLAUDE.md and `.claude/settings.json` (agent-dx, medium, S).
- Fix stale D101 conversion text in ARCHITECTURE.md:361 and PLAN.md (agent-dx, medium, S).
- Unverified, medium, effort M (not quick): endpoint/form/page/migration recipes.

Cheap low-severity items worth bundling in the same change: gitignore `.claude/worktrees/` and add it to the neutrality script and eslint ignores.

### Refactors worth a PR

- Client-scoped write tests for payroll routes (medium, M).
- Guard `db:migrate`, `test:api`, import-excel, bootstrap against remote targets (medium, M).
- Shared ApiError message helper plus guarded `run()` helper in libs (low, M).
- `AuditEntry.ForExport`, `PayrollScope.Period`, `uploadPersonDocument` (low, S each).
- `npm run check` and CI parity (low, S).
- Guard unit tests and route-spec rewrite (low, S).
- Access-rule allow-list test for `AnySignedInUser` and an architecture test for Recruiting/Payroll (low, M).

### Leave alone

- Duplicated Firebase email-link adapter (deliberate under the documented neutrality rule).
- Migration SQL helper duplication (frozen migrations).
- Manual E2E layer (documented choice; API integration tests cover most behaviour).
- Port 5433/5434 "inconsistency" (local override, documented via `.env`).
- ClockTests / catalog-defs trivial assertions, string-literal mutants in navigation.ts, and the equivalent mutants in money.ts.
- Public API ingress itself (intended); only document the `/internal` decision.
- Oversized files, test factories and the OpenAPI-generated types (rated good).

## 5. Corrections after the fix pass

- **Structured logging** ("no JSON console or Serilog configured") was wrong: `Program.cs` already uses Serilog with `RenderedCompactJsonFormatter` on the console. No change needed.
- **Terraform `deletion_protection`:** the database already defaulted to protected (off only in `dev.tfvars.example`); the unconditional `false` was on the Cloud Run API service and migrate job, now `var.cloud_run_deletion_protection` (default true).
- Fixed in branch `chore/audit-quick-wins`: email-log bodies, D101 docs, worktree ignore, CLAUDE.md/settings, DB/remote script guards, `npm run check`, scoped payroll write tests, ports/env/glossary, shared error/run/upload helpers, audit/period/status backend helpers, import-boundary lint + architecture test, guard tests and route specs, decision-log status column, test gaps (and a `-0` in `compareDecimal`), grant/period helpers, recipes.
- Not done (low value or unverified): seed.mjs per-slice flags, CI mirroring local DB setup / `infra:check`, terse reporters, the `/internal/*` ingress decision (needs an owner decision: LB rule or private service).
