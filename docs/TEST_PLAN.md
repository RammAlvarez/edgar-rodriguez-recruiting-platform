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
10. P5 / D69: invite an Administrador with only *Asistencia* → after setting the password the sidebar shows only Calendario and Asistencia; `/clientes` and `/catalogos` typed in the address bar go back to the home screen.

### S4 — Reclutamiento
Data: `npm run seed` (S3 data plus 13 vacantes, 32 candidatos, two notes). Recruiters sign in with `<name>@local.test` / `plataforma-local`. API-level: `DemoPathTests` walks demo steps 1–3c; the portal half of step 3 (the client sees the name) is S5's gate.
1. *Vacantes*: 13 rows; HEB's two open CAJERO vacantes show the solicitud date; GTS *Cubierta*, NM FIRE *Pausada*, CONSTRUCTORA HFC *Cancelada*.
2. «Nueva vacante» HEB / Auxiliar de limpieza / 3 → «HEB – AUXILIAR DE LIMPIEZA», *Abierta*, and in HEB's client detail (Vacantes tab).
3. Pipeline → «Nuevo candidato» (crear nueva persona) → the card in Nuevo, the vacante *En proceso*.
4. Drag a card to Apto, to No apto (motivo asked), to Contratado (fecha de ingreso asked; toast «… : n de m cubiertas»; the vacante *Cubierta* at its headcount).
5. Candidate detail: a note with an H2 and a bulleted list; upload a PDF to a document (browser → MinIO), Validar → completeness changes; Entrevistas → programar; Historial lists the moves.
6. *Reclutadores* → Mónica Soto → «Asignar vacante» as Líder (note «… pasará a Apoyo») → «Ver como reclutador»: banner, recruiter sidebar, the vacante in Mis vacantes; move a candidate → Historial «Edgar Rodríguez (cubriendo a Mónica Soto)» → «Salir».
7. Sign in as monica.soto@local.test: only Mis vacantes / Mis candidatos with her data; typing /reclutadores goes back home.
8. 390 px: no page-level horizontal scroll on Mis vacantes, the vacante detail, Mis candidatos and the candidate detail.

### S5 — Candidates app + Clients app (gate 1)
Data: `npm run seed` (adds the Cliente user heb@local.test). Email links come from the Auth emulator (`GET :9199/emulator/v1/projects/demo-platform/oobCodes`); open the `continueUrl` with the `oobCode` on the app's own domain. Invitations are written to the API log locally (no `RESEND_API_KEY`). API-level: `DemoPathTests.Gate_1_…`, `CandidatePortalTests`, `ClientPortalTests`.
1. Admin «Nuevo candidato» on an HEB vacante with an `@local.test` email → the API log shows «Tu postulación: …» with the Candidates app URL.
2. Candidates app: request a link, open it → aviso de privacidad first; accept → Mi perfil.
3. Mi perfil: type CURP and celular, Guardar → saved; the admin candidate detail shows them (birth date from the CURP).
4. Mis documentos: «Subir foto o PDF» on INE (browser → MinIO) → *Entregado*.
5. Mi postulación: the vacante, tracker on Recibida, the recruiter's contact.
6. Admin: INE *Entregado* → Validar; «Reenviar invitación» toast; move to Apto.
7. Clients app as heb@local.test: Mis vacantes cards (no sueldo, Sexo or Edad); the vacante shows the candidate by name with sueldo ofrecido and documentos %, earlier stages as initials.
8. Post a comment → it appears in the thread, in Comentarios, and in the admin «Comentarios del cliente» tab.
9. 390 px: no page-level horizontal scroll on every screen of both apps.

### S6 — Reportes de reclutamiento
Data: `npm run seed` (13 vacantes, 32 candidatos). Sign in as the owner (edgar@local.test), who holds `recruiting_reports`. API-level: `RecruitingReportTests` (every figure of the six reports on a hand-computed dataset, filters, grant, Excel/PDF exports and their `audit_log` row), `S3ObjectStorageTests.An_export_the_api_writes_…` (MinIO).
1. Reportes → Rendimiento por reclutador (default *Año en curso*): one shaded total row per recruiter (vacantes asignadas, cubiertas, % efectividad, días promedio), then her vacantes; a filled vacante shows its días para cubrir and the total row the same average.
2. Each of the other five reports opens with figures that agree with each other (vacantes, candidatos and contratados totals of Resumen = Vacantes por cliente total row = Σ Fuentes).
3. Cliente = HEB and «Este mes» → the report re-runs with the filters; report 3 hides the range («situación al …»).
4. Exportar Excel / PDF → a file named «<reporte> <desde> a <hasta>» downloads from the `exports` bucket; it shows the same figures as the screen; `audit_log` has an `export` row.
5. 390 px: no page-level horizontal scroll on any of the six reports (tables scroll inside their card).

### S7 — Personal
Data: `npm run seed` (3 payroll groups, 5 puestos, 32 employees: two vacations, an incapacidad, a baja, two queued card requests, one expired account). API-level: `BankAccountRuleTests`, `PayrollGroupTests`, `EmployeeTests`, `ScheduleTests`, the candidate account test in `CandidatePortalTests`, `S3ObjectStorageTests` unchanged.
1. Personal lists the ECOVIA, HFC and HEB staff with puesto, turno, ingreso, status pill (period dates under it) and documents %; «Solicitar tarjeta» only for the employee without an account or request; «Tarjeta solicitada · Banorte» for the queued ones.
2. «Alta de empleado» with a new person and «Solicitar tarjeta» → the detail opens; Cuentas de pago shows the card request.
3. «Cambiar estatus» → Vacaciones today … +4 → header pill «Vacaciones · dd/mm – dd/mm»; five locked attendance days; «Reactivar» → Activo, the days are gone.
4. «Agregar cuenta» with a wrong card digit → the field error; with the right one → «•••• 1237 · Principal», the card banner disappears.
5. Documentos uses «Empleado — expediente»; Horario: a weekly schedule; Calendario day / week / month shows it, with vacations and incapacidad on the expected people.
6. Clientes (nómina) → ECOVIA: VALIDADORES 12 H, its puestos and rates, bonuses off.
7. A Contratado candidate → «Convertir en empleado» pre-fills the hire date and the bank; afterwards the candidate shows «Ver empleado».
8. Candidates app → Mi perfil → «Cuenta para tu pago» adds a card, shown as «•••• 1237».
9. 390 px: Personal, employee detail, Calendario, payroll client detail.

### S8 — Asistencia + jobs
Data: `npm run seed` (adds supervisor@local.test — Administrador, Asistencia for ECOVIA only —, the current and next periods, today's ECOVIA attendance and yesterday's entered through the unlock path). API-level: `AttendanceTests` (scope, lock moment, unlock rules, status-period days, holidays), `JobTests` (each job twice, zones, period close with automatic faltas, through `/internal/jobs`), `PeriodCalendarTests`.
1. As the owner, Asistencia today → a site → «Todos presentes», one Falta, one Incompleto with hours → Guardar → «n/n» and check marks.
2. The previous day → «Este día ya cerró»; «Cambiar» on someone without capture → status and reason → the row shows the new value; `attendance_unlocks` has the row.
3. As supervisor@local.test in a 390 px frame: the menu shows only Calendario and Asistencia; the ECOVIA site roster wraps, vacation and incapacidad rows are read-only; change one person and Guardar; yesterday is closed with no «Cambiar».
4. Calendario día → «Capturar asistencia» opens that site and date; the employee detail's Asistencia tab lists the last days with a lock on closed ones.

### S9 — Incidencias + Prenómina
Data: `npm run seed` on a fresh database (S9 block: ECOVIA's employees are the SEM 38 rows; Semana 38 loaded through the unlock path, closed by `attendance-lock`; the seed fails unless the grid totals 334,654.83 payable and 334,655.149146 exact on 115 lines). API-level: `PrepayrollTests` (SEM 38 golden through the API, incidencias, ajustes, retroactivo to the next open period, paid-period freeze, rates reach open periods only, exports, grants), `AttendanceSummariesTests` (D84).
1. Prenómina → ECOVIA Semana 38 (En revisión): 115 personas, Total neto a pagar $334,654.83; the client's columns K–U; footers under each column and the note that they are informative; payment columns with bank, last four and state.
2. Tap a SUELDO cell → desglose with the exact items; «Ajustar celda» to another amount with a reason → the cell shows it, the row is marked, the total moves by the difference; remove the ajuste from the desglose.
3. Incidencias → Nueva incidencia: Tiempo extra doble 2 h for someone → the row's TIEMPO EXTRA DOBLE and total move; a Descuento shows negative.
4. Excel, Resumen PDF and Recibos download; the Excel's row 5 matches the client's B5:W5.
5. «Tarjetas por solicitar» lists the two queued people by name; 390 px: Prenómina and Incidencias have no page-wide horizontal scroll (the grid scrolls inside).

### S10 — Archivos de pago + Tarjetas
Data: `npm run seed` on a fresh database (S10 block: the paying account MAXIMA REPARACION Y LIMPIEZA SA DE CV with a generated 14-digit Azteca number — never the client's; the two queued card requests' people get CURP, RFC, address and phone so an alta file can be generated; Semana 38 stays *En revisión* so the browser check pays it). API-level: `PaymentFilesTests` (readiness by line, «Generar archivos de pago» → rows only for lines with a valid principal account and an amount > 0, the period frozen as Pagada with each line's account snapshot, the Azteca file read back in the bank's layout with its total = Σ rows, the cuentas frecuentes companion only for unregistered accounts, «Subido al banco» → rows and lines paid and accounts registered, rechazado → re-included by «Archivo de pendientes» with the fixed account, deposit date, `decrypt_account` rows, attendance and status periods refused on paid dates, grants `payroll_payment_files` / `payroll_prepayroll` and client scope), `CardBatchTests` (preview issues, Banorte NOMINATIVA with sucursal and no card number, Banregio with an empty Tarjeta, employee numbers from the per-bank sequences and kept per employee, moot requests cancelled, uploaded, «Tarjeta recibida» → principal debit account, batch completed, grant `payroll_card_requests`), layout tests (Banregio without a card number).
1. Prenómina → ECOVIA Semana 38 (En revisión) → «Pagar»: listos 112 with their amount, 2 tarjeta en trámite and 1 sin cuenta by name; the cuenta de cargo shown by its last four digits.
2. «Generar archivos de pago» → confirm → the period reads Pagada and no cell or comment can be edited; the Azteca file and the cuentas frecuentes file download; the Azteca file opens with the bank's headers in row 1, one row per payment, importe in E, and its total equals the panel's «en este archivo»; the grid's TOTAL NETO A PAGAR is unchanged.
3. «Marcar subido al banco» → rows Pagado; mark one row Rechazado → it appears in Pendientes; record the deposit date.
4. Tarjetas → Por solicitar → Banorte: select the queued person, sucursal → preview without errors → «Generar alta» → the file opens in `Masiva_xls` with NOMINATIVA, the sucursal and no card number; «Marcar subido» → «Tarjeta recibida» with a valid 16-digit number → the person's principal account is that card.
5. Back in Semana 38 → «Archivo de pendientes» → a second file with that person and the rejected row only.
6. Asistencia / unlock on a date of Semana 38 is refused («El período ya se pagó»).
7. 390 px: the payment panel and the Tarjetas panel have no page-wide horizontal scroll.

### S11–S12
Written when each slice starts, from the "Done when" column of PLAN §2 and the demo path of ARCHITECTURE §9.
