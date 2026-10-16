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

### S1–S2
Pure libraries: covered by the xUnit golden and layout tests, no screens.

### S3 — Core registry
Data: `npm run seed` (owner, 11 clients, 6 recruiters, LFT holidays, puestos). Emails are written to the API log (`RESEND_API_KEY` empty).
1. *Usuarios* lists the owner («tú», last access set) and the six recruiters («Invitación pendiente»).
2. «Invitar usuario», rol Administrador → *Permisos delegados* has every box checked except Reportes de reclutamiento; «Enviar invitación» → toast, the row appears without reloading, the API log shows the email with a password-setup link.
3. *Clientes* lists the 11 seed clients with zona and contacto principal; a row opens the detail with tabs Vacantes / Contactos / Sitios.
4. Contactos → «Invitar al portal» → confirm → the contact shows «Con acceso al portal», cannot be deleted, and a Cliente user appears in Usuarios.
5. «Nuevo cliente» with an existing short name → «Ya existe un cliente con ese nombre corto.» in the panel.
6. *Clientes (nómina)*: empty «proyectos activos», «Todos los clientes» with search, every row «Sin proyecto»; a row opens the payroll detail (empty groups / puestos).
7. *Catálogos*: tabs for every catalog; the 51 municipios; «Agregar festivos de ley» adds the year once.
8. Document upload/download: S3 has no people screen (PLAN S3 lists none), so the check is API-level — `DocumentTests` (presign → confirm → versions → review → logged view/download) and `S3ObjectStorageTests` (presigned PUT/GET against MinIO). The browser check moves to the S4 candidate detail.
9. 390 px (carried from S0 step 6).

### S4–S12
Written when each slice starts, from the "Done when" column of PLAN §2 and the demo path of ARCHITECTURE §9.
