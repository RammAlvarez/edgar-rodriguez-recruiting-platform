# Test runs and defect register

## Runs

| Run | Slice | Result |
|---|---|---|
| [2026-10-16-01](2026-10-16-01-s0-foundation.md) | S0 Foundation | pass after fixing D-001, D-002 (D-003 found in review, fixed) |

## Defects

| ID | Found in | Description | Status |
|---|---|---|---|
| D-001 | 2026-10-16-01 | After a successful sign-in the admin app stayed on *Entrar*: the session reported "signed-out" until `/v1/me` answered, so the guard bounced the user back. | fixed — the session goes to "loading" as soon as the provider reports a new user (`libs/ui/src/session.ts`) |
| D-002 | 2026-10-16-01 | Material Symbols showed as text (e.g. "da", "wo"): the theme set the icon font's variation settings but not its `font-family`. | fixed — full icon class in `libs/ui/styles/_platform-theme.scss` |
| D-003 | review after 2026-10-16-01 | A user whose role has no screen in the app (a Cliente in the admin app) would loop between `/` and the first screen: `accessGuard` redirected to `/`, which redirected back. | fixed — role outside the app → *Sin acceso*; `homePath` returns null (test in `navigation.spec.ts`) |
| D-004 | `npm run test:api` during S1 | With `ASPNETCORE_ENVIRONMENT=Development` (set by `scripts/run.mjs`) an unreadable body — malformed JSON or money sent as a JSON number — returned 500: minimal APIs throw `BadHttpRequestException` in Development and the exception handler mapped it to 500. The S0 runs had no environment set, so the test passed. | fixed — `AddPlatformProblemDetails` (`Platform.Core/Http/ProblemResponses.cs`) always throws and keeps the exception's status; test for Development and Production |
| D-005 | `npm run test:api` during S3 | A person captured as *Masculino* was stored as *Indistinto*: EF Core omits a column whose value equals the CLR default (`Gender.Male` is 0) when the column has a database default, so the default won. Same latent risk for any enum default whose first member is valid (`document_status`). | fixed — every column default is declared `.HasDefaultValue(v).HasSentinel(v)` (`EntityConventions`); test `An_enum_value_of_zero_is_stored_…` fails without it |
