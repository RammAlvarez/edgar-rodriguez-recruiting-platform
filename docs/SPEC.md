# SPEC — Plataforma de Reclutamiento y Prenómina

> **Client:** Edgar Rodríguez (CPE Rodríguez) — Recruiting Services + Professional Services (limpieza, mantenimiento, personal eventual), Nuevo León, México
> **Status:** Functional specification v1 · **Last updated:** 2026-10-07
> **Companion docs:** [DATA_MODEL.md](./DATA_MODEL.md) · [ARCHITECTURE.md](./ARCHITECTURE.md)
> **Language convention:** docs and code in English; product UI, catalogs and domain glossary in Spanish (es-MX).

---

## 0. Sources of truth

This spec was derived from, in order of authority:

1. The session brief (modules, roles, tech stack, general requirements).
2. The client's working files in `examples/`:
   - `Sistema_Reclutamiento_Completo_2026.xlsx` — current recruiting system (vacantes, candidatos, reclutadores, catálogos, dashboard).
   - `PRENOMINA SEM 38 12 HRS ECOVIA VALIDADORES.xlsx` — weekly pre-payroll for client ECOVIA; the calculation model.
   - `Alta_Nomina_Masiva Banorte 21Ago-1.xlsx` — Banorte "Alta Masiva NH" layout (Portal BEM) + field rules + catalogs.
   - `ALTA BANREGIO EARL 19AGO26.xlsx` — Banregio bulk card/account registration layout.
   - `Lo HFC MAESTRO 14AGOSTO.xlsx` — Banco Azteca **massive payment** layout, used as a master list of beneficiaries for client HFC (amount column left blank until each run).
   - `cuentasfrec21sept.xlsx` — Banco Azteca "cuentas frecuentes" (beneficiary registration) stub; 2 rows.
3. Meeting transcript *Edgar Rodríguez — Explicación de Exceles* (2026-09-25).
4. `QUOTE_INITIAL_SPEC.md` — inspiration only; superseded where it conflicts.

`archived-do-not-use/` is ignored.

---

## 1. Vision

One platform, two business lines, one shared core.

| Line | Today | Tomorrow |
|---|---|---|
| **Reclutamiento** | Excel with 5 tabs, shared by WhatsApp; clients ask for status by phone | Pipeline per vacante, recruiter workspace, client portal with live progress |
| **Servicios profesionales / Nómina** | Weekly Excel per client; attendance reported on Fridays from memory; bank data read by eye from a comments column; Banco Azteca file hand-typed | Daily, locked attendance capture → live pre-payroll grid with payment columns → one button produces the Banco Azteca file (and card-request files for new hires) |

The shared core is a **people registry** (one record per person, candidate or employee) with a **document vault** (RFC, CURP, Acta de Nacimiento, NSS, INE, comprobante de domicilio, CLABE/tarjeta, …). A candidate marked *Contratado* becomes an employee without re-capturing anything — that conversion is the narrative of the demo.

### What matters most to the client (verbatim priorities)

1. Keep track of candidate and staff documents.
2. Professional Services staff control.
3. Job openings and candidates control.
4. Monitoring of personnel incidents at different clients (attendance, salaries, deductions) for pre-payroll purposes.

### Non-goals (v1)

- Full payroll engine: CFDI 4.0 / timbrado, IMSS/INFONAVIT filings, ISR calculation. The system produces **pre-nómina** numbers that the accountant validates.
- Direct bank API integration. The system **exports the exact Excel layouts** the banks already accept; the client uploads them to the bank portal.
- Biometric time clocks, GPS check-in, job-board auto-posting.

---

## 2. Applications, users and roles

Three Angular apps, one API, one database.

| App | Audience | Auth | Purpose |
|---|---|---|---|
| **Admin app** (`admin`) | Owner + internal staff | Email/password, optional MFA | Reclutamiento, Nómina, Usuarios |
| **Candidates app** (`candidates`) | Candidates | Email link (passwordless) | Fill personal data, upload documents, see own status |
| **Clients app** (`clients`) | The client's customers (ECOVIA, HEB, HFC, …) | Email link (passwordless) | Read-only progress of their job openings and applying candidates |

### 2.1 Roles

| Role | App | Access |
|---|---|---|
| **Administrador** | Admin | Everything: Reclutamiento, Nómina, Usuarios, Reportes. Can grant *permisos delegados* (see 2.2). |
| **Reclutador** | Admin → section *Reclutador* | Only the vacantes assigned to them and the candidates of those vacantes. No access to Reportes, Clientes, Nómina or Usuarios. |
| **Cliente** | Clients app | Only their own vacantes and candidates (row-level scoping by `client_id`). Candidate personal documents are never shown; personal data is anonymized until the candidate is marked *Apto* (see 4.6). |
| **Candidato** | Candidates app | Only their own profile and documents. Not a managed "Usuario" — created automatically when a recruiter captures the candidate with an email. |

### 2.2 Delegated permissions (from the transcript)

The owner wants to hand specific tabs to specific people ("alta masiva Banorte — un botón, le doy permiso a uno de mis muchachos") and keep *Reportes* for himself only. Rather than inventing more roles, an **Administrador** user can have an optional set of **permission grants**, each scoped to a module and optionally to a client:

```
module ∈ { recruiting, recruiting_reports, payroll_prepayroll, payroll_attendance, payroll_incidents,
           payroll_staff, payroll_payment_files, payroll_card_requests, users }      (DATA_MODEL enum permission_module)
client_id ∈ { null = all clients, <client> }
```

- A freshly created Administrador has **all** grants (owner behaviour).
- The owner can uncheck grants to produce e.g. "prenómina ECOVIA only" (review the grid, no payment files) or "payment files only".
- `recruiting` covers Reclutamiento (Inicio, Vacantes, Candidatos, Reclutadores, Clientes) and catalog edits; it is checked by default, and unchecked for a field supervisor (D19, PLAN D69).
- `recruiting_reports` is unchecked by default for anyone except the owner (decision from the meeting: reports are password-protected and personal).

### 2.3 Authentication and security

- Firebase Auth as identity provider, used only behind an `AuthProvider` interface (ARCHITECTURE §2). Role, grants and client scope live **in the database** (`users`, `user_permissions`), never in token claims; the apps learn them from `GET /v1/me` and the API checks them per route (DATA_MODEL §8).
- MFA (TOTP) **optional per user** in the Admin app; the owner can make it mandatory for Administradores from Usuarios.
- Email-link sign-in for candidates and clients; the link is bound to the app's domain (`candidates.*` / `clients.*`), so a client link cannot open the admin app.
- Session: provider token (1 h) refreshed silently by the client adapter; the API verifies it on every request and loads the user row.
- All personal data access is logged (who viewed/downloaded which document) — see ARCHITECTURE.md §6.

---

## 3. Shared core

### 3.1 People registry (`people`)

One record per physical person, keyed by CURP when available (duplicate detection on CURP and RFC). A person may be, over time: candidate for several vacantes → employee at one or more clients → inactive.

Fields captured (aligned with the bank layouts so exports never need re-capture):

| Group | Fields |
|---|---|
| Identity | nombre(s), apellido paterno, apellido materno, fecha de nacimiento, estado de nacimiento, género (*Masculino / Femenino / Indistinto*), nacionalidad (flag *extranjero*), CURP, RFC, NSS |
| Contact | celular (10 dígitos), correo electrónico |
| Address | calle, número, colonia, código postal, municipio, estado |
| Banking | one or more **payment accounts** (banco, tipo *tarjeta de débito / CLABE / cuenta propia*, número, **titular** — may be a third party —, alias, principal, vigencia) — numbers encrypted at rest, masked in UI |
| Misc | fuente de reclutamiento, observaciones |

### 3.2 Document vault

Per-person checklist driven by a configurable **document template** per vacante / position. Mexican employment set shipped by default:

| Código | Documento | Vigencia |
|---|---|---|
| `INE` | Identificación oficial (INE/IFE) | expires (fecha de vigencia) |
| `CURP` | CURP | — |
| `RFC` | Constancia de Situación Fiscal | recommended refresh yearly |
| `NSS` | Número de Seguro Social / IMSS | — |
| `ACTA_NAC` | Acta de nacimiento | — |
| `DOMICILIO` | Comprobante de domicilio | expires (≤ 3 months) |
| `ESTUDIOS` | Comprobante de estudios / certificaciones | — |
| `CUENTA_BANCO` | Estado de cuenta / carátula CLABE | — |
| `ANTECEDENTES` | Carta de antecedentes no penales | expires |
| `RECOMENDACION` | Cartas de recomendación | — |
| `CV` | Currículum | — |
| `FOTO` | Fotografía | — |
| `CONTRATO` | Contrato firmado (staff only) | — |

Each document instance: file (PDF/JPG/PNG, ≤ 10 MB), status (`pendiente` / `entregado` / `validado` / `rechazado` / `vencido`), fecha de vencimiento, who validated, rejection reason. A **completeness %** per person is derived from the template. Candidates upload from the Candidates app; recruiters/admins can upload on their behalf and validate.

### 3.3 Clients (`clients`)

Companies served by either or both lines (today: EL CAMINO, GTS, FILM, ECOVIA, NM FIRE, LUNACO, HEB, CERRO DEL FRAILE, I CALIZA, CONSTRUCTORA HFC, JINSEONG TECH). Fields: razón social, nombre corto, RFC, contactos (nombre, correo, celular, puesto), sites/sucursales (address, zona), active flag. A client contact with portal access becomes a *Cliente* user.

Clients are created from either module with the same side panel and **no module selector**: "Nuevo cliente" in *Reclutamiento → Clientes* sets `uses_recruiting`, "Nuevo cliente" in *Nómina → Clientes (nómina)* sets `uses_payroll`. A client created in one module becomes usable in the other the moment it gets its first vacante or payroll group there; both flags are derived usage markers, never asked of the user.

### 3.4 Catalogs (editable from the Admin app)

Zonas (municipios de Nuevo León + custom), Puestos, Bancos (with clave SPEI: 012 BBVA, 014 Santander, 072 Banorte, 058 Banregio, 127 Azteca, 002 Banamex, 030 Bajío, …), Prestaciones, Niveles de estudio, Fuentes de reclutamiento, Prioridades, Estatus de vacante, Etapas de candidato, Conceptos de nómina, Tipos de documento, Días festivos.

### 3.5 Notifications

In-app bell + **email only** (Resend via API); no WhatsApp in v1. Triggers: document expiring in 30/7 days, candidate uploaded a document, vacante without activity for N days, attendance not captured by cut-off, period ready to close, user invited.

---

## 4. Module: Reclutamiento (Admin app — Administrador)

### 4.1 Vacantes

Mirrors `BD_VACANTES` 1:1 so the owner recognizes his own system.

| Field | Notes |
|---|---|
| Nombre visible | **Not an ID.** Everywhere in the app a vacante is shown as **"Cliente – Puesto"** (e.g. *HEB – Auxiliar de limpieza*); when the same client has more than one open vacante for the same puesto, the solicitud date is appended (*HEB – Auxiliar de limpieza · 02/10/2026*). The Excel's `VAC-0001` code is imported as `legacy_code` and never displayed |
| Fecha de solicitud | default today |
| Cliente | FK |
| Zona | catalog (municipio) |
| Puesto | catalog / free text |
| Vacantes solicitadas | int ≥ 1; the vacante is *Cubierta* when contratados = solicitadas |
| Sueldo mensual | decimal |
| Prestaciones | multi-select from catalog ("de ley", aguinaldo extra, bonos, vales…) + free text |
| Horario | text |
| Banco de pago | multi-select: banks the hiring client pays through (**critical**: workers decline offers if they owe that bank) |
| Nivel de estudios | catalog |
| Años de experiencia | int |
| Experiencia requerida | text |
| Sexo | *Indistinto / Masculino / Femenino* — **internal only**, never shown to clients (defaults to *Indistinto*) |
| Edad | range text — **internal only** |
| Prioridad | Alta / Media / Baja |
| Estatus | **Abierta → En proceso → Cubierta**, side exits **Pausada**, **Cancelada** |
| Fecha cubierta | set automatically when status becomes Cubierta |
| Días abierta | computed: `(fecha_cubierta ?? hoy) − fecha_solicitud` |
| Reclutador líder | FK user (role Reclutador); extra recruiters can be assigned |
| Observaciones | text, history kept as timeline notes |

Views: list with filters (cliente, estatus, reclutador, prioridad, zona), detail with pipeline summary (count per etapa), candidates tab, documents template tab, activity timeline. Actions: assign recruiter, change status (reason required for Pausada/Cancelada), duplicate vacante, share with client (toggle visibility in the Clients app).

### 4.2 Candidatos

Mirrors `BD_CANDIDATOS`. A **candidate** is a *person applying to a vacante* (one person may apply to several; each application has its own stage).

| Field | Notes |
|---|---|
| Nombre visible | the person's name; the Excel's `CAN-0001` is imported as `legacy_code` and never displayed |
| Fecha de captura | default today |
| Persona | from people registry (search by name/CURP/phone; create inline) |
| Vacante | FK, chosen from a dropdown listing *Cliente – Puesto* (open/in-process only, searchable) → cliente, puesto and reclutador are inherited (read-only) |
| Fuente | Facebook / LinkedIn / WhatsApp / Referido / Bolsa de trabajo / Otro |
| Etapa | **Nuevo → Contactado → Entrevista → Apto / No apto → Contratado / Descartado** (see 4.3) |
| Apto | Sí / No (derived from etapa, kept as a column for the report) |
| Contratado | Sí / No + Fecha de ingreso |
| Días de proceso | computed: `(fecha_ingreso ?? hoy) − fecha_captura` |
| Sueldo ofrecido | decimal |
| Comentarios | recruiter notes, timeline |

Views: Kanban by etapa per vacante (drag & drop), table with filters, candidate detail. **Candidate detail layout:** header (name, vacante, cliente, etapa pill, reclutador); left column: *Datos personales* card (teléfono, correo, domicilio, CURP, RFC, fuente…) and below it the **Notas** card; right column: tab group *Documentos* (checklist with completeness %) · *Entrevistas* · *Historial* (stage history). Duplicate warning if CURP/RFC already exists.

**Notas (rich text).** A list of dated notes per candidate, newest first, each with author (and "cubriendo a …" in cover mode), editable by its author or an Administrador. Editor toolbar: H1, H2, H3, Bold, Italic, numbered list, bulleted list, hyperlink — nothing else (no images, tables or colours). Content is stored as sanitized HTML plus the editor's JSON; links open in a new tab; `@` is plain text (no mentions in v1). Notes are internal: never visible in the Clients app or the Candidates app. The legacy free-text `COMENTARIOS` column from the Excel is imported as the first note. Action **"Convertir en empleado"** when Contratado: creates the `employees` record for the vacante's client, copying person + documents (see 5.1).

### 4.3 Candidate stages

| Etapa | Meaning | Side effects |
|---|---|---|
| Nuevo | captured, not yet contacted | invitation email to Candidates app (if email) |
| Contactado | first contact done | — |
| Entrevista | interview scheduled/held; date, interviewer, outcome notes | visible to client portal as "en entrevista" |
| Apto | approved for presentation to client | `apto = Sí`; client portal now shows full name and summary |
| No apto | rejected after interview | `apto = No`; reason required |
| Contratado | hired | `contratado = Sí`, fecha de ingreso required; vacante counter +1; enables *Convertir en empleado* |
| Descartado | withdrawn / no-show / rejected by client | reason required |

### 4.4 Reclutadores

Internal users with role Reclutador (today: Mónica Soto, Pricila de León, Belem González, Servando Moreno, Aarón Vázquez, Aarón Brandon). Admin screen: list, create/invite, activate/deactivate, see assigned vacantes and live KPIs.

**Asignar vacantes.** From the recruiter detail, *Asignar vacante* opens a searchable list of open/in-process vacantes ("Cliente – Puesto", with current líder and days open) where the Admin picks one or several and the role: **Líder** (becomes `lead_recruiter_id`; the previous líder, if any, stays as apoyo unless removed) or **Apoyo** (added to `job_opening_recruiters`). The recruiter's *Vacantes asignadas* table has per-row actions *Hacer líder* / *Quitar*; removing the líder requires choosing a new one. The vacante detail offers the symmetric *Asignar reclutador*. Assignments are stamped with `assigned_at` (the `FECHA_ASIGNACION` of the Excel) and appear in the vacante's Actividad and in the recruiter's *Mis vacantes* immediately; a notification is sent to the recruiter.

**Ver como reclutador (cover mode).** From a recruiter's detail the Administrador opens that recruiter's workspace exactly as she sees it — *Mis vacantes* and *Mis candidatos* filtered to her assignments, same Kanban, same forms — and can act in it (move stages, capture candidates, schedule interviews, upload/validate documents, write notes). A persistent banner shows "Viendo como Mónica Soto · Salir". It is not impersonation: the Admin keeps their own identity and permissions; the UI only applies the recruiter scope. Every write made in this mode is audited as *actor = Admin, acting_for = recruiter* and shown in timelines as "Edgar (cubriendo a Mónica Soto)"; the Clients app shows only the covered recruiter ("Mónica Soto", PLAN D79). Candidates captured in cover mode are assigned to the recruiter (`candidates.recruiter_id`), so her performance report stays hers. Reclutadores cannot use this mode on each other.

### 4.5 Clientes (recruiting view)

Same `clients` entity as the core; this screen adds the recruiting angle: vacantes per client, fill rate, average días abierta, portal users, "invite contact to portal". The client detail's *Vacantes* tab has its own **"Nueva vacante"** button that opens the same vacante drawer with Cliente preset and locked; the new vacante appears in the tab and in the global Vacantes list. Likewise *Contactos* → "Nuevo contacto" and *Sitios* → "Nuevo sitio".

### 4.6 Clients app (Guest portal)

Per client, read-only:

- **Mis vacantes**: cards with estatus, solicitadas vs contratadas, días abierta, reclutador líder (name + phone), count of candidates per etapa.
- **Vacante detail**: candidate list. Before *Apto*: initials + fuente + etapa + fecha de captura only. From *Apto* on: full name, sueldo ofrecido, fecha de entrevista, documents completeness % (never the documents themselves). The vacante card never shows the internal Sexo/Edad fields.
- **Comentarios**: a thread per vacante between the client and the recruiter (optional, cheap, high perceived value).
- No exports, no personal documents, no salaries of other candidates.

### 4.7 Reportes (Administrador with `recruiting_reports`)

Same layout as Nómina reports: a **report selector** on the left, filters on top (**Cliente** Todos/one, **Reclutador** Todos/one, **Rango de fechas** with presets *Este mes / Trimestre / Año en curso* — default *Año en curso*, like the DASHBOARD tab; report 3 is a snapshot with no date), Excel/PDF export. The whole section is behind `recruiting_reports` (owner only, D5).

| # | Report | Content | Source tab |
|---|---|---|---|
| 1 | **Rendimiento por reclutador** | per reclutador × vacante: cliente, puesto, fecha de asignación, presentados, aptos, contratados, % conversión (`contratados / presentados`), días para cubrir; per-recruiter total row: vacantes asignadas, cubiertas, % efectividad, días promedio | `BD_RECLUTADORES` |
| 2 | **Resumen de reclutamiento** | vacantes registradas / abiertas / en proceso / cubiertas / canceladas / pausadas; candidatos capturados / aptos / contratados; conversión global; días promedio de cobertura; the same per client; Δ vs previous equal range | `DASHBOARD` |
| 3 | **Vacantes abiertas y envejecimiento** | every open/in-process vacante: cliente, puesto, prioridad, solicitadas vs contratadas, días abierta, candidatos por etapa, último movimiento, reclutador; flag *sin actividad N días*; sorted by age | new |
| 4 | **Embudo por etapa** | for the range: candidates entering each stage (Nuevo → Contactado → Entrevista → Apto → Contratado), drop-off per stage with motivo (No apto / Descartado), días promedio por etapa; filterable by reclutador and cliente | new (stage history) |
| 5 | **Fuentes de reclutamiento** | per fuente: candidatos, aptos, contratados, % conversión, días promedio a contratación | new |
| 6 | **Vacantes por cliente** | per client: vacantes solicitadas, headcount solicitado vs contratado (fill rate), canceladas, días promedio de cobertura, candidatos presentados | new |

Deferred: quality of hire (contratados que causan baja ≤ 30 días, by recruiter/source — needs the payroll link), sueldo ofrecido vs vacante, talent pool of aptos no contratados.

### 4.8 Reclutador workspace (Admin app — role Reclutador)

- *Mis vacantes*: only vacantes where the user is líder or assigned.
- *Mis candidatos*: Kanban + table limited to those vacantes.
- Can capture candidates, move stages, schedule interviews, upload/validate documents, write notes.
- Cannot: create/edit vacantes (only comment), see other recruiters' data, see Reportes, see salaries of other vacantes.

---

## 5. Module: Nómina (Admin app — Administrador)

"Nómina" in the client's vocabulary means **pre-nómina**: everything needed to know what to pay each worker each period and to generate the bank files. Legal payroll stays with the accountant.

### 5.1 Personal (staff)

An **employee** is a person working for one of the owner's clients (ECOVIA validadores, HFC crews, cleaning staff at offices, …).

| Field | Source |
|---|---|
| Persona | people registry (from a *Contratado* candidate or created directly) |
| Cliente / grupo de nómina | e.g. *ECOVIA — Validadores 12 h* |
| Puesto | per payroll group: VALIDADOR, SUPERVISOR, LIMPIEZA, TÉCNICO, … |
| Turno | C/DÍA, C/NOCHE, custom |
| Fecha de ingreso, fecha de baja, estatus (*Activo / Vacaciones / Incapacidad / Suspendido / Baja*) | `BAJA`, `INCAPACIDAD` appear today as free-text comments; they become states. Vacaciones, Incapacidad and Suspensión are **dated periods** (see *Estatus* below) |
| Número de empleado por banco | Banorte uses `2000000020`-style, Banregio `3000000001`; stored per bank "emisora" |
| Cuentas de pago | one or more accounts with titular, one principal (today in the COMENTARIOS column: "BBVA / 012580015…") — see 5.8 |
| Overrides | SD bolsillo, SD IMSS, costo por hora, bono — default from the position, editable per employee |
| Flags | extranjero, observaciones (pensión alimenticia is not recorded — D16 revised, D50) |
| Documents | shared vault, template "Empleado" |

Views: list (filters: cliente, puesto, estatus, documentos incompletos) — the row's only quick action is **Solicitar tarjeta**, shown when the employee has no payment account and no pending card request (a pending request shows as a "Tarjeta solicitada · Banorte" pill instead); *Baja* is never a quick action. Detail with tabs *Datos / Cuentas de pago / Documentos / Asistencia / Incidencias / Pagos*.

**Estatus.** The detail header shows the status pill next to the employee's name, with the period dates when applicable ("Vacaciones · 14/10/2026 – 20/10/2026"), and an edit button that opens the *Cambiar estatus* panel with: **Vacaciones** (fecha inicio, fecha fin — required), **Incapacidad** (fecha inicio, fecha fin estimada — editable later; folio IMSS optional, no medical detail), **Suspensión** (fecha inicio, fecha fin, motivo), **Baja** (fecha, motivo from catalog, confirmation — irreversible except by *Reingreso*, which creates a new employment), **Reactivar** (ends the current period early). A period creates the matching attendance records for its dates (`vacation` / `sick_leave` / `leave_unpaid`) so the prenómina grid reflects it without supervisor input; when the end date passes, the status returns to *Activo* automatically (scheduler). Status history is kept in the database (audit, Rotación report) but not shown in the detail. Actions: alta, cambio de cliente/puesto (history kept), "Solicitar tarjeta" (Banorte / Banregio) → adds the employee to the *Tarjetas por solicitar* queue in Prenómina (5.5, 5.7). Tab *Cuentas de pago* (5.8) in the employee detail holds all payment accounts and their titulares.

### 5.2 Calendario de servicios

The Professional Services line needs to see **which cleaning/maintenance jobs are scheduled per day and who is expected**:

- **Work schedules**: per employee → client site, shift, days of the week, effective from/to. Recurring (e.g. "Limpieza oficinas HEB, L–V 07:00–15:00") or one-off jobs (mantenimiento 2026-10-12, 3 técnicos).
- **Calendar**: month/week/day; colour by client; "Hoy" view listing each site with expected staff and attendance status.
- The day view is the entry point for attendance capture (5.3).

### 5.3 Asistencia (daily attendance — mandatory, locked)

Decision from the meeting: *attendance is captured every day in the system; if nothing is captured, the person did not work; past days cannot be edited without the owner's permission.*

- One **attendance record** per employee per calendar day: `presente` (with hours; default = shift hours, e.g. 12), `horas incompletas` (hours < shift → paid per hour, see 5.5), `falta`, `descanso`, `descanso laborado`, `festivo`, `festivo laborado`, `incapacidad`, `permiso con/sin goce`, `vacaciones`.
- Capture UI: by client site and date, a roster of expected employees with one-tap status + hours; bulk "todos presentes"; mobile-first.
- Who captures: field supervisors get an **Administrador** account with only the `payroll_attendance` grant scoped to their client (SPEC §2.2) — they see nothing else.
- Cut-off: each record locks at `attendance_lock_time` (default 23:59 of the same day, configurable per payroll group); after lock only an Administrador with `payroll_attendance` can unlock a day, and the unlock + the change are audited (who, when, before/after).
- Missing day = falta automatically when the period closes (explicit record created, flagged `auto`).
- Weekly summary per employee: días laborados, horas extra, festivos, faltas — this is the `DIAS LABORADOS` / `FESTIVO` / `HORAS EXTRA` input of the Excel, now derived.

### 5.4 Control de incidencias

Anything that changes pay and is not plain attendance, captured against an employee and a period:

| Concepto | Type | Example |
|---|---|---|
| Horas extra (dobles / triples) | earning, qty × rate | `=G30*3` |
| Retroactivo | earning, amount | missed pay from a previous period |
| Descanso laborado | earning, fixed | 500 |
| Prima dominical | earning, % of SD (25 %) | `=J9*0.25` |
| Festivo laborado | earning, SD × días × 2 | `=J6*H6*2` |
| Festivo (primera mitad) | earning, 4 h × costo/hora × 3 | VALIDADOR 428.52, SUPERVISOR 585.60 |
| Bono de días trabajados / puntualidad / productividad | earning, formula | `=400/7*días` |
| Descuento | deduction, amount + reason (préstamo, uniforme, daño, adelanto) | |
| Préstamo (abono) | deduction, scheduled | |

Each incident: date, concept, quantity, unit value, amount (decimal), note, created by, evidence file optional. Incidents are listed in the pre-payroll review with a drill-down.

### 5.5 Prenómina (pre-payroll calculation)

Reproduces `PRENOMINA SEM 38` exactly, generalized by **payroll group** (client × scheme) and **position**.

**Payroll group** (`ECOVIA — VALIDADORES 12 h`): client, period type (semanal / quincenal), cut-off weekday (configured per client — ECOVIA's file runs *viernes a jueves*; others may run *miércoles a miércoles*), shift hours (12), active concepts, lock time.

**Position parameters** (from the sheet header):

| Puesto | Sueldo real (SD bolsillo) | SD IMSS | Costo × hora | Bono días trabajados |
|---|---|---|---|---|
| VALIDADOR | 428.57 | 278.80 | 35.71 | 450 |
| SUPERVISOR | 585.71 | 278.80 | 48.80 | 600 |

**Line calculation** (C# `decimal`, **never rounded**: every item is calculated exactly and stored with 6 decimals, truncated (71.4283333… → 71.428333), and the line total is the sum of those items; the only reduction to cents is the worker's **payable amount**, the last calculation, `total_neto_a_pagar = truncate(total_neto, 2)` — truncated, not rounded, e.g. 1,234.5678 → 1,234.56 — which is what the grid's *Total neto a pagar*, the receipt, the bank file and every payment total use; a period's payable total is the sum of its rows' payable amounts, one total that equals the bank file; other columns are displayed truncated to 2 decimals — D42):

```
sueldo              = sd_bolsillo × días_laborados
séptimo_día         = sd_bolsillo × días_laborados / 6
prima_dominical     = sd_bolsillo × 0.25                     (if worked Sunday)
horas_incompletas   = costo_hora × horas_sueltas               (partial shifts)
festivo_1a_mitad    = 4 × costo_hora × 3                       (per holiday in the period, if entitled)
horas_extra         = costo_hora × horas_extra
retroactivo         = Σ incidents(retroactivo)
descanso_laborado   = Σ incidents(descanso_laborado)
tiempo_extra_doble  = costo_hora × 2 × horas_dobles
tiempo_extra_triple = costo_hora × 3 × horas_triples
festivo_laborado    = sd_bolsillo × festivos_laborados × 2
bonos               = Σ bonus concepts active in the group (e.g. 400/7 × días)
descuentos          = Σ deductions (descuento, préstamo)
total_neto          = Σ earnings − descuentos                  (exact)
total_neto_a_pagar  = truncate(total_neto, 2)                  (the only cut to cents)
```

Pensión alimenticia is **not** part of the pre-payroll (D16 revised): SEM 38 row 30 says "PENSION DEL 39%" and pays the full amount.

**Lifecycle — three stages, no manual "Calcular"**

| Stage | Meaning | What the user can do |
|---|---|---|
| **Capturando** | Period created automatically from the group's cut-off; supervisors capture attendance daily, incidents are added as they happen. The grid is **always visible and recomputes live** from attendance + incidents + position rates — there is nothing to "calculate". | Add incidents, unlock a day (audited), edit any cell → *ajuste manual* incident. |
| **Revisión** | Reached automatically when the last day of the period locks (or earlier with "Cerrar captura"). Days with nothing captured become `falta` (`auto`). The period Excel/PDF are available for the nómina clerk and the accountant. | Same edits; each one is logged as "cambio durante revisión". Payment readiness checklist shows what blocks payment (missing account, invalid account length, adjustment without reason). |
| **Pagada** | Pressing **Generar archivos de pago** freezes the lines (snapshot), creates the payment batch and produces the Banco Azteca file(s). Attendance for those dates can no longer be unlocked; incidents dated inside the period go to the next period's `RETROACTIVO` column with a note "corresponde a Semana 38". | Download the files again, mark rows as *rechazado por el banco* and regenerate a file for the pending rows, record the deposit date. |

There is no separate "Cerrado": generating the bank file *is* the close, because nothing may change after a file has been uploaded to the bank. Reopening a paid period does not exist — corrections are retroactivos.

**Grid = the Excel + payment columns.** Same columns and order as `SEM 38` (Fecha ingreso … Total neto a pagar, Comentarios) plus, at the right, the columns that today live inside COMENTARIOS: **Banco**, **Cuenta** (masked, last 4), **Tipo de cuenta** (tarjeta de débito / CLABE / cuenta Azteca), **Titular** (only if not the employee) and **Estado de pago** (Listo / Sin cuenta / Cuenta inválida / Pendiente / Pagado / Rechazado). A warning icon on the row explains any blocker. Comments remain free text (`EXTRANJERO`, `AGREGAR PRIMA`…) but no longer carry bank data.

**Generar archivos de pago** (single button, Revisión only, grant `payroll_payment_files`):

1. Validates every line: amount > 0, account present, digit length matches the account type (14/16/18/20), receiving bank code known.
2. Builds the **Banco Azteca pagos masivos** file from the paying account MAXIMA REPARACION Y LIMPIEZA SA DE CV (fixed header: cuenta de cargo, titular, RFC, leyenda "PAGO", MXP) with one row per payable line: clave del banco receptor, tipo de cuenta (`1` / `3` / `40`), número de cuenta, nombre del beneficiario (= titular of the primary account), leyenda, tipo de pago (`3` traspaso BAZ when the receiving bank is Azteca, `1` SPEI otherwise — derived, not typed).
3. If any beneficiary account is not yet registered as *cuenta frecuente* at the bank, also builds the **cuentas frecuentes** file for those rows (client tag, name, tipo `03`, account, MXP, bank code) and marks them registered once the user confirms the upload.
4. Lines that fail validation stay in **Pendientes de pago**; after fixing the account, "Generar archivo de pendientes" produces a second file for only those rows. Rows the bank rejects can be marked *Rechazado* and re-included the same way.
5. The period moves to *Pagada*; the batch (file, rows, total, who, when) is stored under the period's **Archivos** tab with status *Generado → Subido al banco* (user ticks it after uploading).

**Accounts in someone else's name.** Workers often cannot or will not receive deposits in their own bank (debts, no account). An employee may therefore have several payment accounts, each with its own **titular**; the one marked *principal* receives the pay and its titular goes into the file as *Nombre del beneficiario* (5.8). The grid shows a **Titular** column only when it differs from the employee's name.

**Altas bancarias (card requests) inside Prenómina.** An employee created without a card is queued automatically in a side panel **"Tarjetas por solicitar"** (count badge). From it the user generates the **Alta masiva Banorte** or **Alta masiva Banregio** file for the queued employees of that bank (5.7), uploads it, and later ticks *Tarjetas recibidas* typing the card numbers; the employee's payment account switches to the new card and the next period pays into it. Until then the employee is paid by SPEI to whatever account they provided, or sits in Pendientes.

Outputs: period Excel in the current layout (so "la muchacha de nóminas" validates the way she does today; one total, the sum of the payable amounts — PLAN D76), PDF summary, per-employee receipt (informative, not CFDI), Azteca payment file(s), cuentas frecuentes file, Banorte/Banregio alta files.

### 5.6 Clientes (payroll view)

Two tables — clients with active projects, then all clients — each row opening the client detail with its payroll groups and positions (rates live only there). "Nuevo cliente" creates a payroll client with no further choices (§3.3).

### 5.7 Alta masiva files (Banorte / Banregio) — generated from the card-request queue

**Banorte** — `Masiva_xls` exactly as Portal BEM expects (15 columns B–P, headers in row 2, data from row 3, no blank rows). Fixed values per batch: `Código nacional de ocupación = 09-TECNICOS`, `Producto = 705-Nómina Banorte 2`, `Tipo de tarjeta = GENÉRICA`, `Sueldo bruto mensual = 100,000.00` (the owner's convention to avoid bank caps), `Sucursal a enviar` empty. Per employee: número de empleado, CURP, correo, celular, calle, número, colonia, CP, número de tarjeta (16), RFC (13). Validation before download (from the bank's own rules sheet): uppercase + accents stripped + Ñ→N, CURP 18-char structure, not foreign-born, 18–99 years old, phone 10 digits not repeated/sequential, ≤ 2 employees per identical address, unique emisora+empleado+CURP, email ≤ 50 chars.

**Banregio** — columns: Nómina (employee no.), Nombre, ApePaterno, ApeMaterno, RFC, CURP, Calle, NumCalle, Colonia, CodPostal, TelefonoCelular, FechaNac (dd/mm/yyyy), Genero, Puesto, CorreoElectronico, Tarjeta (16), Estado de nacimiento (bank's list).

Both: generated from "Tarjetas por solicitar" (5.5), preview with validation errors highlighted, download `.xlsx`, batch saved with status *Generado → Subido al banco → Tarjetas recibidas*. Which bank to request is chosen per employee at alta (default from the client's `BANCO` preference on the vacante), because workers refuse banks they owe money to.

### 5.8 Cuentas de pago del empleado (in Personal)

A tab **Cuentas de pago** in the employee detail. An employee can have any number of accounts; exactly one is **principal** and receives the pay. Each account: banco receptor, tipo de cuenta (`1` cuentas propias / `3` tarjeta de débito / `40` CLABE), número (validated by type: 14/16/18/20 digits), **titular** (defaults to the employee's name; editable — a spouse, a relative, anyone the worker asks to be paid through), alias ("Tarjeta Azteca", "Cuenta de mi esposa"), vigencia (valid from / to, so account changes keep history — the `CAMBIO DE CUENTA BANORTE` comments in the sheet). "Hacer principal" switches the payment destination from the next unpaid period; paid periods keep the snapshot of the account they were paid to. A card received from a *Tarjeta por solicitar* request is added here automatically as principal.

This one structure covers the cases seen in the files without extra entities: pay to the worker's own card, pay to a third party's account, change bank mid-year, and the employee number per bank for the alta files.

Non-employee payees (suppliers, contractors such as UALA, FEDEX, Localiza in the HFC master) are **out of scope for v1** (D25); they stay in the bank portal.

### 5.9 Reportes (Nómina)

One screen: a **report selector** on the left, filters on top, the report body below. Filters: **Cliente** (Todos / one), and a date control that adapts to the report — **Semana** picker (default: last paid period) or **Rango de fechas**; monthly range for Rotación; none for Tarjetas y cuentas. Every report exports to Excel and PDF. Grant: `payroll_prepayroll` (client-scoped grants filter the Cliente list).

| # | Report | Columns / content | Date control |
|---|---|---|---|
| 1 | **Costo de nómina por cliente** | per period and client: empleados pagados, sueldo, séptimo día, prima dominical, horas (incompletas, extra, dobles, triples), festivos, bonos, retroactivos, descuentos, **total neto**; a week-over-week row with Δ $ and Δ %; drill-down to the period grid | Semana or Rango (multi-week shows one row per week + total) |
| 2 | **Resumen de pago** | per period: total generado, subido al banco, pendientes, rechazados; breakdown by banco receptor and tipo de pago (traspaso BAZ vs SPEI) with count and amount; list of pending/rejected rows with blocker | Semana |
| 3 | **Incidencias del período** | every incident: fecha, empleado, concepto, cantidad, importe, motivo, capturó, origen (Asistencia / Incidencia / Ajuste manual), "corresponde a" for retroactivos; subtotal per concept | Semana or Rango |
| 4 | **Asistencia por empleado** | per employee: días laborados, faltas, faltas automáticas, incapacidad, permisos, horas incompletas, horas extra, festivos laborados; toggle to an employee × day grid per site (P / F / I / D / H…) with locked-without-capture days highlighted; sortable by faltas | Semana or Rango |
| 5 | **Rotación** | per client and month: altas, bajas, bajas ≤ 30 días, headcount inicio/fin, antigüedad promedio, motivo de baja distribution; list of altas/bajas with origin (vacante / directo) | Rango de meses |
| 6 | **Tarjetas y cuentas** | snapshot: empleados sin cuenta, pagados a cuenta de tercero (titular ≠ empleado), solicitudes de tarjeta por banco y estatus, cuentas no registradas como frecuentes | none |

Out of scope by decision: documentos vencidos (lives as a filter in Personal), IMSS/legal reports, charts beyond the Δ indicators.

## 6. Usuarios (Admin app)

Create/invite users, assign role (Administrador / Reclutador / Cliente), link Cliente users to a client, toggle delegated grants for Administradores, force MFA, deactivate, resend invitation, see last login. Candidates do not appear here. Create and edit both happen in the same side panel (no inline row expansion): clicking a user opens *Editar usuario* with the same fields as *Invitar usuario* plus Activo, Requerir MFA, Reenviar invitación and Desactivar.

---

## 7. Candidates app

Passwordless. First visit: the aviso de privacidad (PLAN D77, P8); then the candidate completes the profile the recruiter started (name, phone, address, CURP, RFC, NSS, bank account for future payroll — the bank account arrives with payment accounts in S7), uploads the documents required by the vacante's template (camera on mobile), and sees: estado de mi postulación (stage, in friendly words), documentos pendientes/validados/rechazados (with reason), reclutador de contacto. Nothing else. Spanish only, mobile-first.

---

## 8. General requirements

| Requirement | Decision |
|---|---|
| Responsive | Desktop-first for Admin (tables, Kanban); mobile-first for Asistencia capture, Candidates app and Clients app |
| Robust accounting | Typed and payable money as `numeric(12,2)`, computed pre-payroll amounts as `numeric(18,6)` (6 decimals, never rounded; payable = truncated to cents once per worker, D42), decimal strings on the wire and C# `decimal` in the engine; no floats anywhere; every pay line traceable to attendance + incidents; closed periods immutable; manual adjustments are incidents with author |
| Language | es-MX UI, dates `dd/MM/yyyy`, currency `$ 1,234.56 MXN`, time zone America/Monterrey |
| Security | Firebase Auth behind `AuthProvider`, optional MFA for Admin, roles/grants in the database checked per route, client scoping in every query, encrypted card/CLABE numbers (AES-GCM in the API), presigned URLs for documents (15 min), append-only audit log written in the same transaction, personal-data read log, audited attendance unlocks |
| Audit | `audit_log` for create/update/delete on people, documents, attendance, incidents, periods, bank batches, users |
| Backups | the managed Postgres's automated backups + PITR (or `pg_dump` cron on the same-VM variant); bucket versioning |
| Performance | ≤ 2 s for the pre-payroll grid at 500 employees; exports streamed |
| Browser support | Last 2 versions Chrome/Edge/Safari; iOS Safari and Android Chrome for mobile |

---

## 9. Decisions log

| # | Decision | Source |
|---|---|---|
| D1 | One platform, shared people registry + document vault, two modules | brief / quote spec |
| D2 | Stack: Angular 22 SPAs · **ASP.NET Core 10 (.NET 10 LTS)** API in a container (`decimal` for all money) · PostgreSQL · S3-compatible storage · Firebase Auth behind an interface · cron container for jobs · env-var config. Provider-neutral; GCP is the first provider (Cloud Run, Cloud SQL over TCP, Firebase Hosting, Cloud Storage S3 API). Supersedes the brief's Cloud Scheduler / Secret Manager (ARCHITECTURE D33–D39, 2026-10-16) | brief + architecture redesign |
| D3 | Pre-nómina only, no CFDI/timbrado | brief / quote spec |
| D4 | Attendance captured daily, locked after cut-off, unlock audited | meeting 2026-09-25 |
| D5 | Reports restricted to the owner via permission grant | meeting 2026-09-25 |
| D6 | Delegated per-module / per-client grants for Administradores instead of more roles | meeting 2026-09-25 + brief (3 roles) |
| D7 | Bank files are generated as the exact Excel layouts, no bank APIs | meeting 2026-09-25 |
| D8 | Banorte alta defaults: CNO 09-TECNICOS, producto 705, tarjeta GENÉRICA, sueldo bruto mensual 100,000 | meeting + file |
| D9 | Banco Azteca massive payment master pre-loaded per paying client; run = beneficiary + amount | meeting 2026-09-25 |
| D10 | Vacante keeps *Banco de pago* as a first-class field | meeting 2026-09-25 |
| D11 | Client portal anonymizes candidates until *Apto* | quote spec, kept |
| D12 | Docs/code in English; UI/catalogs in Spanish | Ramm's convention |
| D13 | Weekly cut-off day is configured **per client** (per payroll group) | answers 2026-10-07 (Q1) |
| D14 | `FESTIVO PRIMERA MITAD` is a rule: **4 hours × hourly rate × 3** (4 h paid at triple rate). Checks against the sheet: VALIDADOR 35.71 × 12 = 428.52; SUPERVISOR 48.80 × 12 = 585.60 | answers 2026-10-07 (Q2) |
| D15 | `MONTO DE HORAS INCOMPLETAS` = partial-shift hours paid at the hourly rate (positive earning) | answers 2026-10-07 (Q3) |
| D16 | ~~Pensión alimenticia is a percentage of net pay~~ **Revised 2026-10-16 (PLAN D50):** pensión alimenticia is not deducted in the pre-payroll and is not recorded; the golden SEM 38 pays row 30 in full | answers 2026-10-07 (Q5), revised 2026-10-16 |
| D17 | MAXIMA REPARACION Y LIMPIEZA SA DE CV is the **owner's own company**: one paying account (Banco Azteca) pays workers of all clients; beneficiaries are grouped by client for filtering only | answers 2026-10-07 (Q6) |
| D18 | `cuentasfrec` (frequent-accounts registration) export is kept | answers 2026-10-07 (Q7) |
| D19 | Field supervisors capture attendance with **Administrador accounts limited by grants** (`payroll_attendance`, scoped to their client); no fourth role | answers 2026-10-07 (Q8) |
| D20 | Clients **do** see the sueldo ofrecido of candidates from *Apto* on | answers 2026-10-07 (Q9) |
| D21 | Notifications by **email only** (no WhatsApp integration in v1) | answers 2026-10-07 (Q10) |
| D22 | Sexo and Edad on vacantes are **internal only** — never shown in the Clients app or in exports shared with clients | answers 2026-10-07 (Q11) |
| D23 | Prenómina and bank files are **one module**: the period's output is the payment file. Lifecycle Capturando → Revisión → Pagada; the grid recomputes live (no "Calcular"); generating the Azteca file freezes the period; corrections after payment are retroactivos | 2026-10-07 |
| D24 | **Everyone is paid from the Banco Azteca account** of MAXIMA REPARACION Y LIMPIEZA SA DE CV; Banorte/Banregio altas only obtain cards (destination accounts), they are never dispersión files | 2026-10-07 |
| D25 | No separate payee entity: an employee has one or more **payment accounts**, each with its own titular (may be a third party), one of them principal. Non-employee payees (suppliers) are out of scope for v1 | 2026-10-07 (revised) |
| D26 | Payment data (banco, cuenta, tipo) are columns of the prenómina grid, not free text in Comentarios | 2026-10-07 |
| D27 | Reportes de nómina = one selector screen with six reports (costo, pago, incidencias, asistencia, rotación, tarjetas), filters Cliente + Semana/Rango adapting per report; documentos vencidos removed from reports | 2026-10-07 |
| D28 | Reportes de reclutamiento = selector with six reports (rendimiento, resumen, vacantes abiertas, embudo, fuentes, por cliente); filters Cliente + Reclutador + Rango with presets, default año en curso | 2026-10-07 |
| D29 | Administrador can open a recruiter's workspace ("Ver como reclutador") and act in it to cover for them; writes audited with `acting_for`, candidates attributed to the recruiter | 2026-10-08 |
| D30 | No system codes in the UI: vacantes are displayed as "Cliente – Puesto" (date suffix only to disambiguate), candidates by name; `VAC-`/`CAN-` codes exist only as `legacy_code` for import | 2026-10-08 |
| D31 | Vacantes can be assigned to a recruiter from the recruiter detail (líder / apoyo) as well as from the vacante; removing a líder requires a replacement | 2026-10-08 |
| D32 | Employee status is a dated model: Vacaciones / Incapacidad / Suspensión are periods that pre-fill attendance and auto-expire; Baja is set only from the detail with date + motivo, never from the table | 2026-10-08 |

## 10. Open questions for the client

| # | Question | Impact |
|---|---|---|
| Q12 | `FESTIVO PRIMERA MITAD` in SEM 38: the rule (D14, 4 h × costo/hora × 3) gives 428.52 / 585.60, and the sheet types 428.5 / 585.7 on 39 rows, but 249.50 on rows 65, 83, 99, 117 and 178.60 on rows 81, 85, 87. Why did those seven get less (partial holiday shift? manual cap?) | the engine applies D14 to every entitled row; SEM 38 comes out $1,466.02 higher than the sheet until answered |
| Q4 | Which bonus scheme is paid today? The current `SEM 38` tab declares `BONO DIAS TRABAJADOS` 450 / 600 in the header but no row pays it; the older `Formula` tab pays `BONO PUNTUALIDAD Y ASISTENCIA = 400/7 × días` and `BONO PRODUCTIVIDAD = 360/7 × días`. Options: (a) none, (b) fixed 450/600 under a condition (e.g. 6 días), (c) puntualidad + productividad per day | which concepts are active in the ECOVIA group; demo grid columns |
| Q13 | Banco Azteca *cuentas frecuentes*: the sample file (`cuentasfrec21sept.xlsx`) has `03` in column C and again in column J, and both rows are debit cards. What do C and J hold for a CLABE (18 digits) or an Azteca own account (14/20)? | until answered the platform registers only debit cards as cuentas frecuentes (PLAN D64); CLABE accounts must be registered by hand |
| Q14 | Banco Azteca *pagos masivos*: in `Lo HFC MAESTRO 14AGOSTO.xlsx` column E (between LEYENDA DEL CARGO and MONEDA) has no header and is empty; we take it to be the **importe**. Is that right, and does the bank expect a header text there and a number or a text cell? | the generated file writes the payable amount in E as a number with 2 decimals and leaves E1 blank, as in the master |

## 11. Delivery plan (suggested)

| Sprint | Scope | Demo moment |
|---|---|---|
| 0 | Infra, auth, roles, users, catalogs, clients, people, document vault | invite a user, upload a document |
| 1 | Reclutamiento: vacantes, candidatos (Kanban), reclutador workspace, Candidates app | capture candidate → candidate uploads INE |
| 2 | Clients app + Reportes de reclutamiento (selector + 6 reports) | client sees progress live; owner opens Rendimiento por reclutador |
| 3 | Personal, calendario, asistencia diaria con bloqueo | capture a day from a phone |
| 4 | Incidencias + prenómina live grid (ECOVIA) + Excel/PDF | reproduce SEM 38 to the cent |
| 5 | Generar archivos de pago (Azteca + cuentas frecuentes), Pendientes, Tarjetas por solicitar (Banorte/Banregio), Cuentas de pago con titular | from the period grid, download the file and upload it to the bank |
| 6 | Reportes de nómina (selector + 6 reports), notificaciones, MFA, hardening, go-live | run "Costo de nómina" for ECOVIA, 4 weeks |
