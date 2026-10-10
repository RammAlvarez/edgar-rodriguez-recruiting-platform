# QUOTE INITAL SPEC — SISTEMA DE RECLUTAMIENTO Y PRENÓMINA Platform

> **Status:** Pre-sales demo stage · **Last updated:** 2026-08-27
> **Purpose of this document:** This was the initial spec, use it as inspiration but none of this is final.

---

## 1. Vision

One platform, two modules, one shared core.

The lead runs two business lines that today live in spreadsheets, WhatsApp, and paper folders:

1. **Recruiting Services** — sourcing and placing candidates against client job openings.
2. **Professional Services** — operating cleaning/maintenance/seasonal crews at client sites.

Both lines share the same underlying problems: **people + documents + client visibility**. The platform is a single web app with two functional modules sitting on a shared core (people registry, document vault, clients, notifications). A candidate hired through Recruiting can be converted into Professional Services staff with one click — their documents follow them. That conversion story is the "wow" of the demo: it shows we understand their business as one pipeline, not two silos.

## 2. Users & Roles

| Role | Who | Access |
|---|---|---|
| **Admin** | Owner / ops manager | Everything, incl. payroll & settings |
| **Recruiter** | Internal recruiting staff | Recruiting module + shared core |
| **Supervisor** | Field supervisors (Prof. Services) | Daily jobs, attendance capture, incidents (mobile-first) |
| **Payroll** | Admin staff | Pre-payroll, incidents review, staff docs |
| **Guest (Client)** | The lead's clients | Read-only portal scoped to *their* openings / *their* sites. No candidate personal documents — only pipeline progress and anonymized profiles until offer stage |

Notes: Guest access is per-client, invitation-based (magic link), and is a key differentiator — clients see live progress instead of asking for status by phone.

## 3. Modules

### 3.0 Shared Core
- **People registry** — one record per person, whether candidate or staff (lifecycle: candidate → hired → active staff → inactive).
- **Document vault** — per-person checklist of Mexican employment documents:
  - RFC (Constancia de Situación Fiscal)
  - CURP
  - Acta de Nacimiento
  - NSS (Número de Seguro Social / IMSS)
  - INE (identificación oficial)
  - Comprobante de domicilio
  - Comprobante de estudios / certificaciones
  - Carta de recomendación / antecedentes no penales (configurable per job)
  - Estado de cuenta (CLABE) for payroll deposit
  - Each document: file upload, status (pendiente / entregado / vencido / rechazado), expiry date where applicable, and an at-a-glance **completeness %** per person.
  - Configurable **document templates per job type** (e.g., seasonal staff needs fewer docs than a placed executive).
- **Clients** — companies served by either or both business lines; contacts, sites, portal invitations.
- **Notifications** — in-app + email: missing/expiring documents, interview reminders, attendance not captured by cut-off time, pre-payroll period closing.

### 3.1 Recruiting Module
- **Job Openings** — per client: description, vacancies count, salary band, required documents template, status (abierta / pausada / cerrada), SLA/target fill date.
- **Candidate pipeline (Kanban)** — stages: Postulado → Filtro / Screening → Entrevista RH → Entrevista Cliente → Documentación → Oferta → Contratado (rejected/withdrawn at any stage with reason). Drag-and-drop between stages; per-stage notes and scheduling.
- **Candidate profile** — contact info, CV, document checklist, interview history, evaluations, activity timeline.
- **Interview scheduling** — date/time per stage, interviewer, outcome notes.
- **Guest portal (per client)** — their openings, count of candidates per stage, days-open, anonymized candidate cards (initials + fit summary) until the client-interview stage; comment thread per opening.
- *Complementary:* talent pool search (past candidates by skill/zone), duplicate detection by CURP/RFC, source tracking (bolsa de trabajo, referral, social).

### 3.2 Professional Services Module
- **Service calendar** — month/week/day views of all cleaning & maintenance jobs; color-coded by client/site; "Hoy" dashboard listing today's jobs with assigned crew and status (programado / en curso / completado / cancelado).
- **Job assignment** — recurring schedules (e.g., "Limpieza oficina Mérida, L-V 7:00") and one-off maintenance jobs; assign staff by availability and site.
- **Attendance (asistencia)** — supervisor checks in crew per job (presente / retardo / falta / permiso / incapacidad*); mobile-friendly capture; daily attendance sheet per site.
- **Incidents (incidencias)** — per staff per client: faltas, retardos, horas extra, bonos, descuentos/deducciones, préstamos — each with date, amount where applicable, and who reported it. This is the raw feed for pre-payroll.
- **Pre-payroll (pre-nómina)** — per period (semanal / quincenal, configurable per client or crew): base salary + overtime + bonuses − deductions − absences, computed from attendance + incidents; review screen with per-person breakdown; export to Excel/PDF for the accountant. *Explicitly NOT a full payroll engine (no CFDI/timbrado in scope) — it prepares the numbers.*
- **Staff management** — profile, site assignments, document vault (shared core), uniforms/equipment log, status.
- *Complementary:* client-site QR check-in (roadmap), service report with photos signed by client contact (roadmap).

*"incapacidad" here only as an attendance category — no medical details stored beyond the IMSS folio.

## 4. Architecture (summary — see architecture diagram)

- **Frontend:** React (TypeScript) SPA — Spanish UI (`es-MX`), i18n-ready.
- **Backend:** NestJS (TypeScript) REST API, modular monolith mirroring the modules above (`core`, `recruiting`, `services`, `payroll`).
- **Database:** PostgreSQL (Cloud SQL). Row-level scoping by client for Guest role.
- **Auth:** Firebase Auth (email/password + magic-link invites for Guests); role claims → NestJS guards.
- **Files:** Google Cloud Storage, signed URLs; documents encrypted at rest; access logged (personal data → LFPDPPP compliance posture).
- **Infra:** Docker → Cloud Run (or GKE later); GitHub Actions CI/CD.


## 6. Out of scope 

Full payroll stamping (CFDI 4.0 / timbrado), IMSS/INFONAVIT filings, biometric time clocks, automated job-board posting.
