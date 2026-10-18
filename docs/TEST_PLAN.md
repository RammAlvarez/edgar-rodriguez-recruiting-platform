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
| Request limits | xUnit, small limits | per address, per person, the four candidate/client routes, forged `X-Forwarded-For`, CORS on 429 (PLAN D92) | `RateLimitTests` |
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

### S11 — Reportes de nómina
Data: `npm run seed` on a fresh database (S11 block: Semanas 39–41 of ECOVIA from the SEM 38 pattern, closed to Revisión; 39 paid and uploaded, 40 with its file generated; the seed fails unless Costo de nómina for ECOVIA over 38–41 has four rows, each equal to its prenómina grid, and Semana 38 at the golden total). API-level: `PayrollReportTests` (Costo = the grid's footers and total with Δ and the default Semana, the export's cells; Resumen de pago against the files; Incidencias priced from the line and subtotalled; Asistencia with automatic faltas and the day grid; Rotación and Tarjetas y cuentas; grant and client scope).
1. Reportes de nómina opens on Costo de nómina with the last paid Semana.
2. Cliente ECOVIA, Rango 11/09/2026 – 08/10/2026 → Semanas 38–41 and a total row; Semana 38 $334,654.83; Δ $ and Δ % from the second row; a row opens that period's Prenómina with the same total.
3. Resumen de pago, Incidencias, Asistencia (with «Por día»), Rotación (default the year so far), Tarjetas y cuentas each show their tables.
4. Exportar Excel and PDF of step 2: the Excel has the title, the filters line and the same rows and total.
5. 390 px: every report without page-wide horizontal scroll (tables scroll inside their card).

### S12 — Notifications, MFA, go-live
Data: `npm run seed` (S12 adds nothing new to the seed but the notifications its jobs and actions create). API-level: `NotificationTests` (each trigger once, no duplicates when a job runs twice, own rows only, mark read, the digest emails each user their unread items once), `MfaTests` (RFC 6238 test vector, enrollment and verification, a forced Administrador refused until the sign-in passed the code, a new sign-in asks again, recovery codes single-use, wrong-code limit, «Restablecer MFA», candidates and client users untouched), the import's pure mappers in Vitest. Import gate: `scripts/import-excel` on an empty `platform_test` through a second API; never on the owner's database (it loads real account numbers, encrypted).
1. The bell shows the unread count; opening it lists them; one opens its screen and turns read; «Marcar todas como leídas».
2. Mi cuenta → Activar verificación en dos pasos: QR and manual key, a code from an authenticator → recovery codes shown once; sign out and in → the code is asked before any screen; a recovery code works once.
3. Usuarios → a user → «Requerir verificación en dos pasos»: at their next request they must enroll; «Restablecer» clears it.
4. Import (`npm run import-excel -- --dir examples` against the second API): the report lists what was loaded and every row left for manual review; Semana 38 created from the workbook has 115 lines and its per-row diff against K–V and V121 shows only the known Q12 festivo rows and the total gap.
5. Same-VM: `docker compose -f deploy/docker-compose.same-vm.yml up` on free local ports → the three apps through Caddy, `/v1` proxied, `/internal` not reachable from outside; migrations run before the API starts.
6. Restore drill (HANDOFF): dump, restore into a scratch database, start the API on it, `/internal/health/encryption` answers ok.
7. 390 px: the bell list and the MFA screens.

### Performance and security (S12 hardening)
500 employees in one group: the prenómina grid, the attendance day, Personal, the payment panel and Costo de nómina each answer in under 2 s on the local stack; findings of the security pass written into the run record.

## 4. Browser checks across the platform

Three runs that cross every slice, run before a release (or after a change that touches more than one slice), each written
up like the slice runs: `docs/tests/YYYY-MM-DD-NN-happy.md`, `-forms.md`, `-e2e.md`. A failed expectation is a `D-NNN`.

Common data: `npm run seed` on a **fresh database** (as S9–S11): ECOVIA Semana 38 *En revisión*, 39 paid and uploaded, 40
paid with its file not uploaded, 41 in review; the HEB Cliente user, the ECOVIA supervisor, the cuenta de cargo. Emails go to
the API log; email links come from the Auth emulator (S5). Jobs are fired by hand, each one safe to repeat (HANDOFF §1):

```
curl -X POST -H "X-Internal-Secret: $INTERNAL_SECRET" http://localhost:5080/internal/jobs/<name>
# attendance-lock · period-roll · status-roll · document-expiry · notifications-digest
```

A fresh database without touching the owner's `platform`: create `platform_<run>` in the Postgres container with
`db/init/roles.sql.inc` (as `db/init/10-roles.sh` does), `npm run db:migrate` with `MIGRATIONS_DATABASE_URL` and
`DATABASE_URL` pointing at it, start the API with that `DATABASE_URL`, then `npm run seed` with the same variable. The seed
waits out the request limit (D92) whenever it hits it, so a fresh seed takes about five minutes.

Quoted text is the exact UI text. Where a step says «the field's error», the run records the message shown; it must be in
Spanish, name the problem, and sit under the field (never a raw status code or an English message).

### 4.1 HAPPY — one pass over every process

One person, one sitting, about 90 minutes. The order follows the information: admin creates, the candidate answers, the
client sees, then a short admin close-out that reads what the other two apps wrote. The payroll half runs on the seeded
ECOVIA weeks so it does not wait on the new hire.

**A. Admin app (owner, edgar@local.test)**
1. *Entrar* with a wrong password → the error line; with the owner → home. Every sidebar item opens a screen (*Inicio* may
   still be the «próxima entrega» placeholder); «Actualizar» is in the toolbar.
2. **Users.** *Usuarios* → «Invitar usuario» «Prueba Asistencia», Administrador, only *Asistencia*, «Solo para el cliente»
   HFC → toast «Invitación enviada a …»; the API log has the password link. Open it in a private window, set the password →
   the sidebar shows only Calendario and Asistencia. Back as the owner: the row shows the last access.
3. **Catalogs.** *Catálogos*: add a Prestación «Bono de asistencia» and a Tipo de documento «Constancia médica» (vence,
   vigencia 365) → both listed (the seed already has «Vales de despensa» and «Cartas de recomendación»: adding one again →
   «Ya existe una prestación con ese nombre.»); «Agregar festivos de ley 2026» → «Los festivos de ley 2026 ya estaban en el
   catálogo» (the seed loads this year and next), nothing added.
4. **Clients.** «Nuevo cliente» *HAPPY SA* with RFC, contacto principal and celular → «Cliente creado»; Sitios → «Nuevo
   sitio» with zona and CP → «Sitio guardado»; Contactos → «Nuevo contacto» with an `@local.test` email → «Invitar al portal»
   → «Con acceso al portal» and a Cliente user in *Usuarios*.
5. **Vacante.** *Vacantes* → «Nueva vacante» HAPPY SA / Auxiliar de limpieza / 2, sueldo, prestaciones (incl. the new one),
   documentos requeridos, Sexo and Edad (internal), «Visible en el portal del cliente» on → «Vacante creada», *Abierta*,
   also under the client's Vacantes tab.
6. **Recruiter.** *Reclutadores* → Mónica Soto → «Asignar vacante» HAPPY as Líder → «Ver como reclutador»: the vacante in Mis
   vacantes → «Salir».
7. **Candidate.** Pipeline → «Nuevo candidato» (new person, `happy.candidata@local.test`, fuente, sueldo ofrecido) → the card
   in Nuevo, the vacante *En proceso*, the API log has «Tu postulación: …». Move to Contactado, Entrevista; Entrevistas →
   programar with Mónica; a note with a list.
8. **Recruiting reports.** *Reportes* → Rendimiento por reclutador shows Mónica with one more vacante; Resumen counts the new
   candidate; Exportar Excel downloads.
9. **Staff.** *Personal* → «Alta de empleado» in HFC with a CLABE → the detail; second «Alta de empleado» without account +
   «Solicitar tarjeta» Banorte → «Tarjeta solicitada · Banorte». On the first: Horario (a weekly schedule), «Cambiar estatus»
   → Vacaciones tomorrow … +2 → the pill; *Calendario* week shows it.
10. **Attendance.** *Asistencia* today → an open site (HFC closes at 20:00, ECOVIA at 23:59; after that the day shows «Este
    día ya cerró») → «Todos presentes» (fills the people without a capture), one Falta, Guardar → «Asistencia guardada ·
    n persona(s)», «n/n». A closed day → «Cambiar» one person with a reason → «Día corregido: el cambio quedó registrado»
    and the new value.
11. **Incidencias.** «Nueva incidencia» Tiempo extra doble 2 h on an ECOVIA employee of Semana 41, and a Descuento → both
    listed.
12. **Prenómina.** ECOVIA Semana 41 → the incidencia's row moved; a SUELDO cell → desglose → «Ajustar celda» with a reason →
    the total moves by the difference; Excel downloads. Semana 38 → «Pagar» → «Generar archivos de pago» → Pagada; the Azteca
    file downloads; «Marcar subido al banco».
13. **Cards.** Tarjetas → Por solicitar → Banorte → the person of step 9 → sucursal → «Revisar» lists what the bank needs
    (CURP, correo, calle, número, colonia, CP, RFC; calle without punctuation) → «Corregir en Personal» → Persona →
    «Editar» → fill them → «Datos actualizados» → «Revisar» clean → «Generar alta» → «Alta Banorte generada: 1 persona» and
    the file; Lotes → «Marcar subido al banco» → «Tarjeta recibida» with a valid 16-digit number → «… la tarjeta ·NNNN es su
    cuenta principal».
14. **Payroll clients.** *Clientes (nómina)*: «Clientes con proyectos activos» lists ECOVIA, HFC and HEB with their groups,
    puestos and active employees (HFC counts step 9's two altas), *Activo* in «Todos los clientes»; HAPPY SA → «Nuevo grupo»
    (semanal, corte domingo) → «Grupo … guardado»; «Nuevo puesto» with tarifas → «Puesto guardado»; Bonos y conceptos →
    «Conceptos guardados».
15. **Payroll reports.** Costo de nómina ECOVIA 38–41 → Semana 38 $334,654.83 and the Semana 41 total equal to step 12's grid;
    Rotación shows step 9's alta; Tarjetas y cuentas the card of step 13.
16. **Bell and MFA.** The owner's bell has the unread items (periods in review, días sin captura); open one → it opens its
    screen and the count drops by one. (The welcome item goes to the invited user, «documento subido» to the vacante's
    recruiter.) Verificación en dos pasos → «Generar código QR» → the code → recovery codes; sign out and in → the code is
    asked.

**B. Candidates app (:4301)**
17. Link for `happy.candidata@local.test` → aviso de privacidad → accept → Mi perfil.
18. Mi perfil: CURP, RFC, NSS, celular, address → Guardar; «Agregar mi cuenta» a debit card → «•••• NNNN».
19. Mis documentos: upload INE (PDF) and comprobante (JPG) → *Entregado*.
20. Mi postulación: the vacante, the tracker on the current etapa, Mónica's contact.

**C. Clients app (:4302)**
21. Link for the HAPPY SA contact of step 4 → Mis vacantes: Auxiliar de limpieza, no sueldo / Sexo / Edad.
22. The vacante: the candidate (initials until Apto); post a comment → in the thread and in Comentarios.

**D. Admin close-out (owner)**
23. The candidate detail shows the CURP, RFC, address and birth date from step 18; the documents *Entregado* → Validar both
    → *Validado*; Mónica has «… subió Identificación oficial» in her bell; «Comentarios del cliente» has step 22's comment.
24. Move to Apto → the client sees the full name, sueldo ofrecido, entrevista and documentos % (reload :4302); «Mover a
    Contratado» with today's fecha de ingreso → «… 1 de 2 cubiertas»; «Convertir en empleado» offers only HAPPY SA's group
    and says «Ya tiene cuenta principal; no se solicita tarjeta» → Convertir → the employee, with step 18's card as
    *Principal*.
25. 390 px: one screen of each app (Prenómina, Mi perfil, the client's vacante) has no page-wide horizontal scroll.

### 4.2 FORMS — stress test of every form

Run each form below against the **checklist**, then the form's own rules. Record per form: pass, or the `D-NNN`.

**Checklist (every form)**
- a. Submit empty → each required field shows its error and nothing is sent (Network: no request).
- b. Max length: type exactly the limit → saved; one more → «Admite hasta N caracteres.» or blocked at N.
- c. Whitespace only in required text → refused (not saved as blank); leading/trailing spaces trimmed on save.
- d. Accents, ñ, `&`, apostrophes (`O'Brien`), emoji and `<script>alert(1)</script>` → stored and shown as typed, never run.
- e. Double-click / Enter twice on the submit → one row, one toast.
- f. Cancelar / Escape / clicking outside after typing → nothing saved; reopen → previous values.
- g. Edit → reopen → the saved values, including selects, dates, toggles and money (`1234.5` shown as `$1,234.50`).
- h. Money fields: `-1`, `1e3`, `1,234.56`, `12345678901` (11 integer digits), `1.234` → «Introduce un importe válido, por
  ejemplo 1234.56.» (or the field's server message); `0` where only > 0 is valid.
- i. Dates: a date before a required start, end before start, 29/02 of a non-leap year typed by hand.
- j. Server down mid-form (stop `npm run api`, submit) → an error message, the panel stays open with the values; start it again
  and submit → saved once.
- k. 390 px: the panel fits, the keyboard does not hide the submit, errors readable.
- l. A second tab edits the same record first → the later save shows the server's answer, not a silent overwrite of unrelated
  fields.

**Admin app**
| Form | Where | Specific checks (on top of the checklist) |
|---|---|---|
| Entrar | `/entrar` | bad email → «Introduce un correo válido.»; empty → «El correo es obligatorio.» / «La contraseña es obligatoria.»; wrong password → «No se pudo iniciar sesión. Inténtalo de nuevo.»; «Olvidé mi contraseña» with no email → «Escribe tu correo para enviarte el enlace.», with one → «Te enviamos un enlace…» |
| Verificación en dos pasos | account menu / `/verificacion` | wrong code → «El código no es correcto o ya se usó…»; same code twice; a used recovery code; 5 wrong codes → 15-minute wait; 7+ digits; letters |
| Invitar / Editar usuario | Usuarios | duplicate email → «Ya existe un usuario con ese correo.»; Cliente role without client → «Elige el cliente al que pertenece el usuario.»; Reclutador with grants → none saved; change the email of an accepted user → «El usuario ya aceptó la invitación…»; desactivar → that user's next request is refused |
| Nuevo / Editar cliente | Clientes | duplicate short name → «Ya existe un cliente con ese nombre corto.»; nombre corto 41 chars; RFC 11 and 14 chars, lowercase → «El RFC tiene 12 caracteres…»; celular 9 digits; notas 2001 chars |
| Contacto | Cliente → Contactos | nombre 121 chars; puesto 81; invalid email; delete a contact with portal access → refused |
| Sitio | Cliente → Sitios | duplicate name in the same client → «Este cliente ya tiene un sitio con ese nombre.» (allowed in another client); CP `6400`, `640000`, `abcde` → «El código postal tiene 5 dígitos.» |
| Grupo de nómina | Clientes (nómina) | duplicate name → «El cliente ya tiene un grupo con ese nombre.»; horas `0`, `25`, `12.555` → «Entre 1 y 24 horas.» / «Horas con hasta 2 decimales.»; prima dominical `1.5` → «Entre 0 y 1 (0.25 = 25 %).»; festivo × `6`; no cierre time |
| Puesto y tarifas | grupo → Puestos | costo por hora `35.71429` (5 decimals) → «Importe con hasta 4 decimales…»; «Vigentes desde» before the current rates → «La fecha debe ser posterior al inicio de las tarifas vigentes…»; a new rate on a paid period's dates → the paid period keeps its amounts |
| Bonos y conceptos | grupo | base días `0`, `32`; importe `-5` |
| Catálogos (each tab) and plantillas | Catálogos | duplicate clave/nombre; vigencia `-1`; «Plantilla por omisión» on two templates of the same kind → only one stays default; deactivate an item in use → still shown on old records |
| Nueva / Editar vacante | Vacantes | vacantes `0`, `1001`, `1.5` → «Indica cuántas vacantes (1 o más).»; sueldo `10000000`; 31 otras prestaciones → «Hasta 30 prestaciones.»; one of 121 chars; años `61`; observaciones 4001; change líder to someone not assigned → «Elige el nuevo líder entre los reclutadores asignados.» |
| Pausar / Cancelar vacante | vacante | empty motivo → «Este campo es obligatorio.»; 501 chars |
| Asignar reclutador / Asignar vacante | vacante / Reclutadores | none selected → «Elige al menos un reclutador.»; an inactive recruiter not offered; two líderes → «Una vacante tiene un solo líder.» |
| Nuevo candidato | pipeline | «Nombre, CURP o celular» finds an existing person → «Usar esta persona»; a new person with an existing CURP or RFC → «Ya existe una persona con esa CURP o RFC.»; CURP 17 chars → «La CURP debe tener 18 caracteres…»; celular 9 → «El celular debe tener 10 dígitos.»; no vacante / fuente → «Elige la vacante.» / «Elige la fuente.»; the same person twice on the same vacante → refused |
| Persona (datos) | candidate / employee detail | fecha de nacimiento not matching the CURP → «La fecha de nacimiento no coincide con la CURP.»; NSS 10 digits → «El NSS tiene 11 dígitos.»; «Extranjero» on → CURP rules relaxed as SPEC says |
| Cambio de etapa | kanban drag / menu | No apto without motivo → «Indica el motivo.»; Contratado without date → «Indica la fecha de ingreso.»; date before captura → «La fecha de ingreso no puede ser anterior a la fecha de captura.»; Contratado beyond the headcount → the vacante stays *Cubierta* and the message says so |
| Nota | candidate → Notas | empty → «Escribe la nota.»; 20 001 chars; pasted Word/HTML → formatting kept, scripts dropped |
| Entrevista | candidate → Entrevistas | no fecha → «Indica la fecha y hora.»; interviewer outside the team → «Elige un entrevistador del equipo.»; a date in the past |
| Documentos (subir, validar, rechazar) | candidate / employee detail | `.docx` → «Sube un PDF, JPG o PNG.»; 10 MB passes, 10.1 MB → «El archivo debe pesar como máximo 10 MB.»; a renamed `.exe` as `.pdf`; the same file twice → «Este archivo ya se registró.»; vencimiento before expedición; an expired one → «El documento ya está vencido; sube uno vigente.»; Rechazar without motivo → «Indica el motivo del rechazo; la persona lo verá.», 301 chars |
| Nuevo líder (al quitar un reclutador) | Reclutadores → detail | quitar the líder of a vacante with apoyos → the dialog asks for the new líder; Cancelar keeps everything |
| Alta de empleado | Personal | no grupo / puesto / turno / fecha → the field errors; CLABE with a wrong digit → «CLABE inválida: el dígito verificador no coincide.»; a CLABE of another bank → «La CLABE es de otro banco (…)»; card with a wrong digit → «Número de tarjeta inválido…»; no account and no «Solicitar tarjeta» → allowed, listed as «Solicitar tarjeta» |
| Agregar cuenta | employee → Cuentas | titular 41 chars, alias 61; second principal → «El empleado ya tiene cuenta principal…» unless «Hacerla la cuenta principal» is checked; 11 accounts in an hour → 429 (bank-account limit 10/h) |
| Asignación (puesto, tarifas) | employee detail | SD and costo por hora as in Puesto; change of group mid-period |
| Horario | employee → Horario | salida before entrada; a one-off date without sitio; more than 42 days in Calendario → «Consulta como máximo 42 días.» |
| Cambiar estatus | employee header | Vacaciones without fin → «Indica la fecha de fin.»; end before start; Baja without motivo → «Elige el motivo de la baja.» or without «Confirmo la baja…»; a Baja on a paid date → refused; Reactivar / Reingreso |
| Captura de asistencia | Asistencia | Incompleto without hours; hours `25`, `-1`; Guardar twice; two people saving the same site and day in two tabs → both captures kept, no 500 (D-013) |
| Cambiar (desbloqueo) | Asistencia, closed day | no motivo → «Indica el motivo del cambio.»; as supervisor@local.test → «Solo un administrador con Asistencia para todos los clientes desbloquea días cerrados.» (or no «Cambiar»); a date of a paid period → «El período de ese día ya se pagó…» |
| Nueva incidencia | Incidencias | no tipo → «Elige el tipo de incidencia.»; horas `0`, `169`, `1.234` → «Escribe las horas (más de 0, hasta 168…)»; importe `0` → «Escribe un importe mayor que cero…»; a date outside the period / of a paid period; an employee of another group |
| Ajustar celda / comentarios | Prenómina → desglose | no motivo → «Escribe el motivo del ajuste.»; the same amount → «La celda ya tiene ese importe.»; `12.345` → «Escribe un importe con hasta dos decimales.»; on a Pagada period → no edit |
| Cuenta de cargo | Prenómina → Pagar | number empty → «Escribe la cuenta de cargo.»; nombre 81, titular 41 chars; RFC 11 chars |
| Pagos (rechazo, fecha de depósito) | payment panel | Rechazado without motivo / 201 chars → «Escribe el motivo del rechazo (hasta 200 caracteres).»; deposit date before the period |
| Tarjetas (alta, recibida) | Tarjetas | sucursal `1234` → «Escribe la sucursal Banorte (5 dígitos)…»; recibida with 15 digits or a wrong check digit; a person without CURP/RFC → preview lists the issue and blocks «Generar alta» |
| Report filters | Reportes, Reportes de nómina | Desde after Hasta; a 5-year range; a client with no data → empty state, exports still open |

**Candidates app**
| Form | Specific checks |
|---|---|
| Entrar (email link) | empty / invalid email messages; an unknown email → the same confirmation (no account disclosure); an expired or reused link → «Usar otro correo»; a link opened in another browser asks for the email |
| Aviso de privacidad | «Continuar» disabled until the box is checked; «Salir» signs out; typing `/perfil` before accepting → back to the aviso |
| Mi perfil | CURP / RFC / NSS / celular / CP rules as in Persona; birth date vs CURP; 31 saves in 10 minutes → 429 and the screen says to wait (profile limit 30/10 min) |
| Cuenta para tu pago | the account rules of Agregar cuenta; «Cambiar cuenta» keeps one principal |
| Mis documentos | the document checks above; a phone photo (HEIC → refused, JPG → ok) at 390 px through the file picker |

**Clients app**
| Form | Specific checks |
|---|---|
| Entrar (email link) | as the Candidates app; a candidate's email → «Sin acceso» |
| Comentario | empty → refused; 2000 chars ok, 2001 → refused; after sending, the box is empty and **not** red (D-009); 31 comments in an hour → 429 (client comments 30/h) |

Rate limits other than those above are covered by `RateLimitTests`; to reach one by hand, lower its `RATE_LIMIT_*` in `.env`
for the run as in run 12, and put it back.

### 4.3 E2E — one week of work

**Why the week is replayed.** The API has no adjustable clock (`SystemClock`), and a day locks at its group's *Cierre de
asistencia* time; past days change only through «Cambiar» with a reason (D83). So the run builds a new client on day 0 and
replays **the last complete week** of its new group day by day, using the unlock path for each past day, then works the
current week live. Every job is fired by hand after the day it belongs to. One sitting of about four hours, or split by
day with the run record noting where it stopped.

Notation: **L0** = the Monday of the last complete week (Lunes–Domingo), **D0** = today. Write the actual dates at the top of
the run record. Sign-ins: the owner unless stated; Mónica Soto (recruiter); a new supervisor created on Día 0.

**Día 0 — setup (D0, live)**
1. *Catálogos*: a puesto «Operador de limpieza» if missing.
2. *Clientes* → «Nuevo cliente» *E2E LIMPIEZA* with contacto principal (`e2e.cliente@local.test`), a sitio *Planta Norte* in
   a zona → «Invitar al portal».
3. *Clientes (nómina)* → E2E LIMPIEZA → grupo «OPERACIÓN» semanal, corte domingo, 8 h, cierre 23:59, divisor 6, prima 0.25,
   festivo × 2. Puestos: OPERADOR (SD bolsillo 300.00, SD IMSS 278.80, costo por hora 37.50, vigentes desde L0 − 7) and
   SUPERVISOR (400.00 / 278.80 / 50.00). Round rates so the run record can work one person's week by hand.
4. *Usuarios* → invite «Supervisor E2E», Administrador, only Asistencia, «Solo para el cliente» E2E LIMPIEZA; set the password.
5. *Personal* → six «Alta de empleado» in OPERACIÓN with fecha de ingreso L0 − 7: four OPERADOR with accounts (one CLABE, one
   Banorte card, one Azteca own account, one paid to a third party as titular), one SUPERVISOR with a CLABE, and one OPERADOR
   **without account** + «Solicitar tarjeta» Banorte (CURP, RFC, address, phone, birth captured). Horario lunes–sábado,
   domingo descanso, at Planta Norte.
6. Expected: Personal lists the six; Calendario week of L0 shows them; Tarjetas → Por solicitar has one Banorte request.

**Lunes (L0, via unlock)** — *Asistencia* → L0 → Planta Norte → «Cambiar» per person, motivo «E2E captura L0»:
7. All present. Recruiting starts: *Vacantes* → «Nueva vacante» E2E LIMPIEZA / Operador de limpieza / 2, líder Mónica, visible
   in the portal (the vacante lives on D0; the week's story continues on it).

**Martes (L0+1)**
8. Five present, one Falta (operador A). Status: operador B «Cambiar estatus» → Vacaciones L0+2 … L0+3 → those days show
   Vacaciones locked in Asistencia.
9. Mónica (her own sign-in) captures three candidates on the vacante: one new person with `e2e.c1@local.test`, one existing
   person found by CURP, one that will be discarded.

**Miércoles (L0+2)**
10. Present except operador B (Vacaciones, read-only). Operador C Incompleto 5 h.
11. Mónica: c1 → Contactado → Entrevista (programar for D0+1); the third → Descartado with motivo.
12. Candidates app as c1: aviso → Mi perfil complete → Mis documentos: INE, comprobante, CURP.

**Jueves (L0+3)**
13. Present; operador D Incompleto 10 h with 2 h extra in the unlock form. Incidencia: Tiempo extra doble 2 h for operador D on L0+3.
14. Operador E: «Cambiar estatus» → Baja on L0+3, motivo, «Confirmo la baja…» → L0+4 onward locked as Baja.
15. Clients app as `e2e.cliente@local.test`: the vacante with c1 as initials; a comment «¿Pueden venir el lunes?».

**Viernes (L0+4)**
16. Present (E is Baja). Incidencia: Descuento 150.00 for operador A (uniforme).
17. Admin: c1's documents → Validar two, Rechazar one with motivo → the candidate sees the rejection and uploads again → Validar.
    Reply to the client in «Comentarios del cliente» is visible in the portal thread.

**Sábado (L0+5)**
18. Present; operador C Descanso laborado instead of Sábado → check the expected rule on the row.

**Domingo (L0+6)**
19. All Descanso except the supervisor: Descanso laborado (prima dominical). If L0…L0+6 contains an LFT festivo, one person
    works it (Festivo laborado ×2) and another rests it.

**Lunes siguiente — close the week**
20. Fire `attendance-lock` → the period of L0 reads *En revisión*; the bell has «período en revisión»; days of the week show
    the lock. If it still reads *En captura*, check the group's cierre time and fire it again.
21. Fire `period-roll` and `status-roll` (twice each → no duplicate periods, B back to Activo once the vacation ended).
22. Fire `document-expiry` and `notifications-digest` → the API log has one digest per user with unread items, and none on
    the second run.

**Review the prenómina**
23. *Prenómina* → E2E LIMPIEZA OPERACIÓN, the week of L0: six lines (E with days up to the Baja), A with one Falta and the
    Descuento, B with Vacaciones, C partial, D with tiempo extra doble, the supervisor with prima dominical.
24. Work operador A's week by hand (SPEC §5.5, PLAN D41–D43) in the run record → equals the desglose of each cell and the row
    total to the cent.
25. Ajustar one cell with a motivo → the total moves by the difference; remove it → back.
26. Rate change: OPERADOR new tarifas vigentes desde D0's Monday → the L0 week **unchanged**, the current period uses the new rate.
27. Exports: Excel, Resumen PDF, Recibos → the Excel's totals = the grid footers.

**Pay**
28. «Pagar»: five listos (the four + supervisor), the tarjeta-en-trámite one by name, nobody «sin cuenta»; cuenta de cargo by last
    four digits.
29. «Generar archivos de pago» → Pagada, nothing editable; the Azteca file (one row per payment, total = «en este archivo»)
    and the cuentas frecuentes file (only unregistered accounts) download.
30. «Marcar subido al banco» → rows Pagado; mark the third-party row Rechazado with motivo; fecha de depósito.
31. Tarjetas → Banorte → «Generar alta» (sucursal) → file with NOMINATIVA and no card number → «Marcar subido» → «Tarjeta
    recibida» → principal account. Back in the week → «Archivo de pendientes» → a file with that person and the rejected
    row only → subido.
32. Freeze: Asistencia «Cambiar» on L0+2 → «El período de ese día ya se pagó…»; a Baja or incidencia dated in the week → refused.

**Live week (D0) — the hire joins**
33. Mónica: c1 → Apto (client sees the full name) → Contratado, fecha de ingreso D0 → «1 de 2 cubiertas»; «Convertir en
    empleado» into OPERACIÓN (bank from the candidate's own account) → appears in Personal and in today's roster.
34. Supervisor E2E at 390 px: sidebar Calendario + Asistencia only; Planta Norte today → capture everyone incl. c1 → Guardar;
    yesterday → no «Cambiar»; ECOVIA is not offered.
35. The bell (owner): «falta captura» for any site left without today's capture after the lock time; «documento por vencer»
    if one was set to expire within the warning window in step 12.

**Reports cross-check**
36. Costo de nómina, E2E LIMPIEZA, week of L0 → one row equal to the grid's TOTAL NETO A PAGAR.
37. Resumen de pago → the payments of steps 29–31 with the rejected-then-paid row.
38. Incidencias → the tiempo extra doble and the Descuento, priced as in the grid.
39. Asistencia (with «Por día») → the days of steps 7–19 exactly.
40. Rotación → six altas at L0 − 7, the Baja of E, the alta of c1.
41. Tarjetas y cuentas → the Banorte card received, the accounts by bank.
42. Recruiting Reportes → Mónica: E2E vacante with one cubierta and its días para cubrir; Fuentes counts the three candidates.
43. Excel and PDF of steps 36 and 42 open with the same figures; `audit_log` has their `export` rows.

**Pass:** every step's expectation met, step 24's hand total equal to the grid, and the six reports agree with the grid and
with each other.
