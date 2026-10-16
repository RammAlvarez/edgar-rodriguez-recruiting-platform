# Test runs and defect register

## Runs

| Run | Slice | Result |
|---|---|---|
| [2026-10-16-01](2026-10-16-01-s0-foundation.md) | S0 Foundation | pass after fixing D-001, D-002 (D-003 found in review, fixed) |
| [2026-10-16-02](2026-10-16-02-s3-core-registry.md) | S3 Core registry | pass; 390 px not run (carried to S4); D-005 found by the API tests |
| [2026-10-16-03](2026-10-16-03-s4-reclutamiento.md) | S4 Reclutamiento | pass after fixing D-006, D-007, D-008; 390 px checked |
| [2026-10-16-04](2026-10-16-04-s5-portals.md) | S5 Candidates app + Clients app | gate 1 pass; D-009 fixed; 390 px checked |

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
