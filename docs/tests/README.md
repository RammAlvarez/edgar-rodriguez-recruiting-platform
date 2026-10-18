# Test runs and defect register

## Runs

| Run | Slice | Result |
|---|---|---|
| [2026-10-16-01](2026-10-16-01-s0-foundation.md) | S0 Foundation | pass after fixing D-001, D-002 (D-003 found in review, fixed) |
| [2026-10-16-02](2026-10-16-02-s3-core-registry.md) | S3 Core registry | pass; 390 px not run (carried to S4); D-005 found by the API tests |
| [2026-10-16-03](2026-10-16-03-s4-reclutamiento.md) | S4 Reclutamiento | pass after fixing D-006, D-007, D-008; 390 px checked |
| [2026-10-16-04](2026-10-16-04-s5-portals.md) | S5 Candidates app + Clients app | gate 1 pass; D-009 fixed; 390 px checked |
| [2026-10-16-05](2026-10-16-05-s6-reports.md) | S6 Reportes de reclutamiento | pass; 390 px checked |
| [2026-10-16-06](2026-10-16-06-s7-personal.md) | S7 Personal | pass after fixing D-010; 390 px checked |
| [2026-10-16-07](2026-10-16-07-s8-asistencia.md) | S8 Asistencia + jobs | pass; capture driven in a 390 px frame |
| [2026-10-16-08](2026-10-16-08-s9-prenomina.md) | S9 Incidencias + Prenómina | pass after fixing D-011, D-012; screens on the S8 data; the seeded Semana 38 gate met on a fresh database |
| [2026-10-16-09](2026-10-16-09-s10-pagos.md) | S10 Archivos de pago + Tarjetas | gate 2 pass; four display fixes in the run; 390 px checked |
| [2026-10-16-10](2026-10-16-10-s11-reportes-nomina.md) | S11 Reportes de nómina | gate pass (fresh database and browser); two fixes in the run; 390 px checked |
| [2026-10-16-11](2026-10-16-11-s12-go-live.md) | S12 Notifications, MFA, go-live | import gate pass; same-VM stack and restore drill pass; 500 employees under 2 s; D-013 fixed; 390 px checked |
| [2026-10-17-12](2026-10-17-12-rate-limits.md) | Request limits (D92) | pass after one fix (blank page while waiting out a 429) |

## Pre-release runs

The three cross-platform checks of [TEST_PLAN §4](../TEST_PLAN.md#4-browser-checks-across-the-platform), run before each
release. Each run is written up as `YYYY-MM-DD-NN-happy.md`, `-forms.md` or `-e2e.md` and gets a row here.

| Run | Check | Result |
|---|---|---|
| [2026-10-17-13](2026-10-17-13-happy.md) | 4.1 HAPPY — one pass over every process | 25 steps pass after fixing D-014 – D-017; plan wording corrected; 390 px checked |
| — | 4.2 FORMS — stress test of every form | not run yet |
| — | 4.3 E2E — one week of work | not run yet |

## Defects

| ID | Found in | Description | Status |
|---|---|---|---|
| D-001 | 2026-10-16-01 | After a successful sign-in the admin app stayed on *Entrar*: the session reported "signed-out" until `/v1/me` answered, so the guard bounced the user back. | fixed — the session goes to "loading" as soon as the provider reports a new user (`libs/ui/src/session.ts`) |
| D-002 | 2026-10-16-01 | Material Symbols showed as text (e.g. "da", "wo"): the theme set the icon font's variation settings but not its `font-family`. | fixed — full icon class in `libs/ui/styles/_platform-theme.scss` |
| D-003 | review after 2026-10-16-01 | A user whose role has no screen in the app (a Cliente in the admin app) would loop between `/` and the first screen: `accessGuard` redirected to `/`, which redirected back. | fixed — role outside the app → *Sin acceso*; `homePath` returns null (test in `navigation.spec.ts`) |
| D-004 | `npm run test:api` during S1 | With `ASPNETCORE_ENVIRONMENT=Development` (set by `scripts/run.mjs`) an unreadable body — malformed JSON or money sent as a JSON number — returned 500: minimal APIs throw `BadHttpRequestException` in Development and the exception handler mapped it to 500. The S0 runs had no environment set, so the test passed. | fixed — `AddPlatformProblemDetails` (`Platform.Core/Http/ProblemResponses.cs`) always throws and keeps the exception's status; test for Development and Production |
| D-005 | `npm run test:api` during S3 | A person captured as *Masculino* was stored as *Indistinto*: EF Core omits a column whose value equals the CLR default (`Gender.Male` is 0) when the column has a database default, so the default won. Same latent risk for any enum default whose first member is valid (`document_status`). | fixed — every column default is declared `.HasDefaultValue(v).HasSentinel(v)` (`EntityConventions`); test `An_enum_value_of_zero_is_stored_…` fails without it |
| D-006 | 2026-10-16-03 | Hired candidates showed negative *días de proceso*: the seed hired them on a date before their captura, and the API accepted a fecha de ingreso earlier than the fecha de captura. | fixed — 400 «La fecha de ingreso no puede ser anterior a la fecha de captura.» (`CandidateTests`); the seed captures after the vacante opened and hires two weeks later |
| D-007 | 2026-10-16-03 | A Reclutadora typing an Administrador address (/reclutadores) got *Sin acceso — tu cuenta no tiene acceso a esta aplicación*, a dead end for someone who does use the app. | fixed — admin routes use `staffGuard` (`app.routes.ts`): staff of the other role go home; *Sin acceso* stays for Cliente users |
| D-008 | 2026-10-16-03 | Making a recruiter líder of a vacante she was not on logged two Actividad entries, «como apoyo» then «como líder». | fixed — the row created for a líder is not logged as an apoyo assignment (`OpeningRules.Assign(log: false)`; `OpeningTests`) |
| D-009 | 2026-10-16-04 | After sending a comment in the Clients app the emptied box turned red as «required»: `form.reset()` keeps the form directive's *submitted* flag, which Material uses to show errors. | fixed — `FormGroupDirective.resetForm()` (`apps/clients/src/app/openings/comment-thread.ts`) |
| D-010 | 2026-10-16-06 | «Cerrar sesión» on the shared «Sin acceso» page signed out but stayed on the page, which then said «Tu cuenta no tiene acceso» again instead of offering to sign in with another email. | fixed — navigates to «Entrar» after signing out (`libs/ui/src/status-pages.ts`) |
| D-011 | 2026-10-16-08 | The grid's Cuenta column read «A nombre de NOE ALANIS TREVINO» for the employee's own account: the holder was compared with the name accents included. | fixed — `PrepayrollQueries.SameName` ignores order, case and accents; test `The_holder_is_the_employee_whatever_the_order_case_or_accents` |
| D-012 | 2026-10-16-08 | «Nueva incidencia» showed an empty Empleado list: `/v1/payroll/incidents/employees` answered 500 (EF cannot sort after projecting into the record). | fixed — sorted before the projection; the pickers are asserted in `PrepayrollTests` |
| D-013 | 2026-10-16-11 | Two writes for the same person and period at once (a capture and an incidencia) recomputed the same prenómina line together; the second answered 500 (DbUpdateConcurrencyException). Found by the 500-employee load run. | fixed — a transaction-scoped advisory lock per line in one global order (`Prepayrolls.RecomputeAsync`, `EnsureLinesAsync`); `RecomputeConcurrencyTests` |
| D-014 | 2026-10-17-13 | `npm run seed` on a fresh database stopped partway (the attendance unlocks after the S8 supervisor) with 429 on `/v1/attendance/unlock`: D92's 600 requests per minute per staff user is below the seed's pace (thousands of attendance days). | fixed — the seed waits out `Retry-After` and repeats the request (`scripts/seed.mjs`); a fresh seed takes about five minutes |
| D-015 | 2026-10-17-13 | An employee given «Alta de empleado» directly could not get a card: the Banorte alta asks for CURP, RFC, correo and domicilio, and «Corregir en Personal» led to a Persona card with no way to edit them. | fixed — «Editar» on the Persona card opens the «Datos personales» panel (`staff/employee-detail.ts`); the view reloads on the `people` tag |
| D-016 | 2026-10-17-13 | Clientes (nómina) still had the S7 placeholder: «Sin proyectos activos» and 0 groups / 0 employees / «Sin proyecto» for every client, ECOVIA's 113 employees included. | fixed — counts from `/v1/payroll/groups` (`clients/payroll-clients-page.ts`); table 1 lists Cliente, grupos, Puestos, Empleados activos. ARCHITECTURE 3f's «Próximo corte» and «Período en curso» are still open |
| D-017 | 2026-10-17-13 | «Marcar como contratado» reloaded the page and saved nothing; so did every motivo prompt (No apto, Descartado, Rechazar documento, Pausar / Cancelar vacante) and the note's hyperlink: the dialog's `<form>` had `(ngSubmit)` without a form directive, so the browser submitted it. | fixed — a `FormGroup` on the form (`openings/prompt-dialog.ts`); `prompt-dialog.spec.ts` fails without it |
