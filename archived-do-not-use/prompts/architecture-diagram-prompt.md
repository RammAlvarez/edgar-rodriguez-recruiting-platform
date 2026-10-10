# Claude Design Prompt — Architecture Diagram (v3)

Create a **cloud architecture diagram** for "Talento MX Platform" — a web platform for a Recruiting + Professional Services business in Mexico. Landscape artboard, 2600×1500. Follow my existing diagram design system for colors, typography, and card styling.

## Layout conventions

- Header top-left: title **"Talento MX Platform"** with subtitle "Arquitectura en la nube · Google Cloud Platform". Top-right legend: solid arrow = "tráfico en tiempo de ejecución", dashed arrow = "programado / respaldo".
- Below the title, a numbered flow strip (①–⑤) summarizing the signature flow: ① reclutador avanza al candidato a "Contratado" → ② se convierte en Personal con un clic → ③ su expediente de documentos lo acompaña → ④ se asigna a servicios y asistencia → ⑤ incidencias alimentan la pre-nómina.
- Groups are labeled containers; nodes are cards with an icon chip, bold title, and 1–2 lines of metadata caption. Edge labels in small pill chips. Left-to-right flow, generous whitespace.
- One outer container wraps everything cloud-side, labeled **"Proyecto GCP del cliente — región: northamerica-south1 (Querétaro)"**. User and email nodes sit outside it, on the far left.

## Content

**Far left (outside the GCP container):**
- Card "Usuarios internos" — "Admin · Reclutadores · Supervisores (móvil) · Nómina".
- Card "Clientes (Invitados)" — "portal de solo lectura · acceso por invitación (magic link)".
- Bottom-left: card "Servicio de correo" — "SMTP / SendGrid" with an "entrega" arrow to card "Correos a personal y clientes" — "notificaciones · documentos por vencer".

**FRONTEND group:** card "React SPA" — "Cloud Run · frontend estático · UI en español (es-MX)", with three small chips inside: "Módulo Reclutamiento", "Módulo Servicios", "Portal de Clientes". Arrows in: "HTTPS" from both user cards.

**BACKEND group:** card "NestJS API" — "Cloud Run · REST · escala a cero", second line: "core · reclutamiento · servicios · pre-nómina". Beside it a slim card "Firebase Auth" — "roles: admin, reclutador, supervisor, nómina, invitado". Arrow "REST / JSON" from frontend; arrow "verificación de tokens" between API and Firebase Auth.

**DATOS group (right of backend):**
- Card "Cloud SQL for PostgreSQL" — "IP privada · conector de Cloud SQL · datos aislados por cliente (RLS)". Arrow from API: "IP privada · conector SQL".
- Card "Cloud Storage — bucket de documentos" — "RFC · CURP · Acta de Nacimiento · NSS/IMSS · INE · CLABE · URLs firmadas · versionado activado" (highlight "versionado activado"). Arrow from API: "carga de documentos".
- Card "Google Secret Manager" — "credenciales de BD · claves SMTP". Arrow from API: "lectura de secretos".

**TAREAS PROGRAMADAS group (bottom center):** card "Cloud Scheduler" — "4 tareas cron → endpoints autenticados del API", with a schedule table inside (time chip + description per row):
- "Diario 07:00" — recordatorio de asistencia no capturada
- "Diario 08:00" — alertas de documentos por vencer (personal y candidatos)
- "Vie 18:00" — corte de pre-nómina (XLSX)
- "Sem / Mes" — exportaciones de respaldo de BD
Dashed arrow up to the API: "HTTPS autenticado · 4 tareas". Dashed arrow to the email service: "correos de notificación". Dashed arrow to the backups bucket: "exportaciones pg_dump".

**RESPALDOS group (far right):** card "Cloud Storage — bucket de respaldos" — "región EE. UU. · bloqueo de retención" (highlight "bloqueo de retención"), plus a list "NIVELES DE CICLO DE VIDA GFS: semanal × 1 mes · mensual × 1 año · anual × 5 años · clase Archive". Dashed arrow from Cloud SQL: "respaldos automáticos diarios".

**Footer strip (row of 4 small cards):**
- "🔒 Datos personales protegidos (LFPDPPP) · acceso auditado"
- "📄 Bóveda de documentos con vencimientos y % de completitud"
- "🔁 Candidato contratado → Personal — un solo expediente"
- "💰 Infraestructura ~$20–40 USD/mes · escala a cero fuera de horario"
