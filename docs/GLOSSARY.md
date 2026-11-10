# Glossary — Spanish UI terms ↔ English identifiers

Docs and code are in English; the product UI, catalogs and domain vocabulary are Spanish (es-MX) (SPEC header). This table maps the
two so neither has to be guessed. When a term is missing, add it here in the same change that introduces it.

## People and roles

| UI term (es-MX) | Identifier | Notes |
|---|---|---|
| Administrador | `UserRole.Administrator` | Everything, limited by *permisos delegados* (`PermissionModule` grants, optionally scoped to one client). |
| Reclutador | `UserRole.Recruiter` | Only the vacantes assigned to them. |
| Cliente | `UserRole.Client` | Clients-app user, scoped to its own `client_id`. |
| Candidato | candidate | Candidates-app user, created when a recruiter captures a candidate with an email; not a managed *Usuario*. |
| Persona | `Person` | The people registry shared by candidates and employees. Reclutamiento and Nómina stay separate businesses (D101). |
| Empleado | `Employee` | Personal (Nómina) only; never created from a candidate (D101). |

## Reclutamiento

| UI term | Identifier | Notes |
|---|---|---|
| Vacante | job opening | A hire fills it (`filled`). |
| Candidatos | candidates | One application per person × vacante. |
| Etapa: Nuevo · Contactado · Entrevista · Apto · No apto · Contratado · Descartado | `CandidateStage`: `New` · `Contacted` · `Interview` · `Suitable` · `NotSuitable` · `Hired` · `Discarded` | *No apto* and *Descartado* need a reason; *Contratado* needs the fecha de ingreso. |
| Apto / Contratado | `is_suitable` / `is_hired` | Follow the stage. |

## Nómina

| UI term | Identifier | Notes |
|---|---|---|
| Grupo de nómina | `PayrollGroup` | Client × payroll scheme (period type, cutoff weekday, shift hours, rules). |
| Puesto | `Position` | Belongs to a group; keeps a rate history (`PositionRate`). |
| Alta / Baja / Reingreso | create employee / `EmployeeStatus.Terminated` / `rehire` | Reingreso is a new employment. |
| Cambiar estatus | status period | `EmployeeStatus`: `Active`, `Vacation` (Vacaciones), `SickLeave` (Incapacidad), `Suspended` (Suspensión), `Terminated` (Baja). |
| Cambio de cliente / puesto | `assignment` | Audited. |
| Horario · Calendario de servicios | `WorkSchedule` · calendar | Weekly or one-off schedule at a client site. |
| Asistencia | attendance (`AttendanceStatus`) | Captured by site and date; locked after the group's lock time. |
| Presente · Incompleta · Falta | `Present` · `Partial` · `Absent` | |
| Descanso · Descanso laborado | `RestDay` · `RestDayWorked` | |
| Festivo · Festivo laborado | `Holiday` · `HolidayWorked` | |
| Incapacidad · Vacaciones | `SickLeave` · `Vacation` | Also attendance values. |
| Desbloquear | `attendance/unlock` | A change with a reason; Administrador whose grant covers every client. |
| Incidencia | `Incident` | Anything that changes pay besides attendance, e.g. `OVERTIME_DOUBLE` (horas extra dobles), `DEDUCTION` (descuento), `REST_DAY_WORKED`. |
| Retroactivo · Descuento | retroactive earning · deduction | |
| Prenómina | pre-payroll (`PayrollPeriod`, `PrepayrollLine`) | The weekly calculation; the golden workbook is SEM 38 (`docs/PLAN.md` D41). |
| Período / Semana N° | `PayrollPeriod` | Weekly period with a number. |
| Línea · Editar celda | `PrepayrollLine` · adjustment | A cell edit is saved as an *ajuste manual* with a reason. |
| Séptimo día · Prima dominical | seventh day · Sunday premium | Rules of the group (`seventhDayDivisor`, `sundayPremiumPct`). |
| SD bolsillo · SD IMSS | daily salary (pocket / IMSS) | Position rates; money is a string on the wire (D46). |
| Costo por hora | `hourlyRate` | |
| Cuenta de pago | payment account | Number encrypted (AES-GCM); only `last4` is ever returned. |
| Titular · Tercero | holder · third party | An account whose holder differs from the person is *de tercero*. |
| CLABE · Tarjeta | account type | Banks: Banorte / Banregio for the card requests. |
| Solicitar tarjeta · Tarjetas por solicitar | card request · card-request queue | `/v1/card-requests`. |
| Alta masiva | bulk-registration file | Banorte / Banregio files built from the card-request queue. |
| Archivos de pago | payment files | Bank layouts produced from a closed prenómina. |
| Cerrar captura | close capture | `PeriodStatus.Capturing` → `InReview`. |
| Pagado | `PeriodStatus.Paid` | A paid period is frozen. |
