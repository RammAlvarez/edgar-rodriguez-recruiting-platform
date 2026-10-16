# TEST PLAN — Plataforma de Reclutamiento y Prenómina

> Companion of [PLAN.md](./PLAN.md). Automated tests run in CI on every push; the browser checks below are run by hand
> (or by an agent driving Chrome) at the end of each slice. Each run is written up in `docs/tests/YYYY-MM-DD-NN-<slice>.md`
> and indexed, with its defects, in [`tests/README.md`](./tests/README.md).

## 1. Automated

| Layer | Tool | What | Where |
|---|---|---|---|
| API rules | xUnit, real Postgres (`platform_test`, connected as `platform_api`), fake `IAuthProvider` | one test per permission-matrix row (DATA_MODEL §8) and per server rule; audit trail; jobs with a fixed `IClock` | `services/platform-api/tests` |
| Identity adapter | xUnit + Firebase Auth emulator | `FirebaseAuthProvider` verifies emulator tokens, creates users idempotently, issues links | same, runs when `FIREBASE_AUTH_EMULATOR_HOST` is set |
| Payroll engine | xUnit, pure | golden SEM 38 (PLAN D41) + one test per concept | `Platform.Payroll.Engine` tests (S1) |
| Bank layouts | xUnit, pure + ClosedXML | headers and rules of the four bank files in `examples/` | S2 |
| Libs | Vitest | money/date formatting, api-client (auth header, errors, X-Invalidates, reviver), Zod schemas | `libs/**/**.spec.ts` |
| Apps | Vitest via `ng test` | navigation/visibility, routes, repositories, pure component logic | `apps/*/src/**/*.spec.ts` |
| Conventions | xUnit | every `/v1` route has an access rule; every mutating route declares cache tags | `RouteConventionTests` |
| Neutrality | `scripts/check-neutrality.sh` + ESLint | vendor SDKs only in their adapter files; no `fetch` outside libs/api-client | CI |

## 2. Local environment for browser checks

```
npm run db:up            # Postgres + MinIO + scheduler (ports from .env)
npm run db:migrate
npm run emulators        # Firebase Auth emulator on :9199
npm run api              # http://localhost:5080
npm run seed             # owner edgar@local.test / plataforma-local (+ sample data from S3 on)
npm start -- admin       # http://localhost:4300   (candidates :4301, clients :4302)
```

## 3. Browser checks per slice

Each check lists the steps and the expected result; a failed expectation becomes a defect `D-NNN` in the register.

### S0 — Foundation
1. Open the admin app signed out → redirected to *Entrar*.
2. Sign in with a wrong password → «El correo o la contraseña no son correctos.»; with the seed owner → *Inicio*.
3. The sidebar shows Inicio, the Reclutamiento, Nómina and Administración sections with their icons (owner: all grants).
4. Account menu shows «Edgar Rodríguez · Administrador»; «Cerrar sesión» returns to *Entrar*; signing in again lands on *Inicio*.
5. «Actualizar» is present in the toolbar.
6. At 390 px wide the sidebar collapses behind the menu button.
7. Candidates and clients apps (:4301, :4302) open on the email-link page; sending a link to an address shows the confirmation.

### S1–S12
Written when each slice starts, from the "Done when" column of PLAN §2 and the demo path of ARCHITECTURE §9.
