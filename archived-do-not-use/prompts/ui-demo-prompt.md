# Prompt — Interactive Web App Demo (v2)

Build a **fully interactive, clickable prototype** of "Talento MX Platform" — a web platform for a Recruiting + Professional Services (limpieza, mantenimiento, personal temporal) business in Mexico. Not static artboards: a single-page web app I can navigate live in front of a client, with working navigation, state, and micro-interactions. All data is realistic mock data held in memory (no backend). All UI copy in **Spanish (es-MX)**, with realistic Mexican sample data (names like Mariana Gutiérrez, Carlos Peña; clients like "Hotel Riviera Kin", "Plaza Altabrisa", "Corporativo GMD"; Mérida/Cancún sites) — never lorem ipsum.

## Demo-critical behaviors

- **Role switcher** in the top bar (Admin / Reclutador / Supervisor / Nómina / Cliente-Invitado): switching roles changes the visible navigation and screens — selecting "Cliente (Invitado)" swaps to the read-only client portal shell. This is the single most important control for the demo.
- Everything reachable by click: sidebar navigation switches views instantly, cards open detail panels, tabs and filters actually filter the mock data.
- **The signature flow must work end-to-end:** drag (or move via button) a candidate to "Contratado" → a "Convertir a Personal" dialog appears → confirming shows the person now in the Personal list with their document expediente intact, plus a success toast ("Expediente transferido — 8/8 documentos").
- Smooth transitions, hover states, toasts on actions; empty states where lists are filtered to nothing.

## App shell

Left sidebar (logo "Talento MX", sections: Inicio, Reclutamiento [Vacantes, Candidatos, Entrevistas], Servicios [Calendario, Asistencia, Incidencias, Pre-nómina], Personal, Clientes, Documentos, Configuración), collapsed groups expandable. Top bar: global search, notifications bell with badge "3" opening a dropdown (documento por vencer, entrevista hoy, asistencia sin capturar), role switcher, avatar "Ramiro A."

## Views

**1. Inicio (dashboard):** 4 KPI tiles (Vacantes abiertas 12 · Candidatos activos 87 · Contrataciones del mes 9 · Documentos por vencer 5 — each clickable, navigating to its filtered view). Widgets: "Vacantes prioritarias" table, "Servicios de hoy" list with estado chips, "Actividad reciente" feed.

**2. Candidatos (Kanban):** vacante selector ("Recamarista — Hotel Riviera Kin (4 vacantes)") + filters (fuente, reclutador) that work. Columns Postulado → Filtro → Entrevista RH → Entrevista Cliente → Documentación → Oferta → Contratado with **drag-and-drop between columns**. Cards: name, initials avatar, fuente chip, document ring ("7/9 docs"), days in stage. Clicking a card opens the candidate profile.

**3. Perfil de candidato (panel or route):** info + WhatsApp button, stage timeline, and the hero — **"Expediente de documentos"** checklist (RFC ✅, CURP ✅, Acta de Nacimiento ✅, NSS/IMSS ⏳, INE ✅, Comprobante de domicilio ❌ rechazado "ilegible", CLABE ⏳, Antecedentes no penales ⚠️ vence 12/09/2026) with completeness bar "6/8 · 75%", per-document status toggling on click, and "Solicitar faltantes por WhatsApp" button (shows toast). "Convertir a Personal" button per the signature flow.

**4. Portal del cliente (Invitado role):** simplified read-only shell for "Hotel Riviera Kin": per-vacante cards with funnel bar per stage, anonymized candidates (initials + fit summary) until "Entrevista Cliente" stage where names unlock, a working comment thread (typing + send appends a message), and note "Acceso de solo lectura · actualizado en tiempo real".

**5. Calendario de servicios:** week view, "Hoy" highlighted, events color-coded by client with cuadrilla avatars and estado chips; client filter checkboxes that filter events; clicking an event opens details (sitio, cuadrilla, checklist del servicio). Banner: "Hoy: 8 servicios · 23 personas asignadas". Week navigation arrows work.

**6. Asistencia:** renders as a centered mobile-width panel ("Vista del supervisor en campo — Limpieza Plaza Altabrisa · 07:00"): crew list with tap toggles Presente / Retardo / Falta / Permiso / Incapacidad that update a live counter on the sticky "Guardar asistencia (6/8)" button; saving shows a toast and marks the service complete on the calendar.

**7. Incidencias:** filterable table (periodo, cliente, tipo chips: falta / retardo / horas extra / bono / descuento / préstamo); clicking a row opens a side panel with detail and working Aprobar / Rechazar buttons that update the row's estado. Header: "Periodo 24–30 ago · 14 incidencias · 3 por revisar" (count updates as you approve).

**8. Pre-nómina:** period selector "Semanal · 24–30 ago 2026 · Cuadrillas Mérida", estado "Borrador". Per-person rows: salario base, horas extra, bonos, deducciones, faltas (−), **neto a pagar**; rows expand to show the incident breakdown behind each number (approving an incidencia in view 7 is reflected here — precompute two consistent states if live linkage is too complex). Footer totals, "Exportar Excel" / "Exportar PDF" (toast "descarga simulada"), "Cerrar periodo" (confirm dialog → estado changes to "Cerrado"). Banner: "Los montos se calculan de asistencia + incidencias · la nómina fiscal (CFDI) la timbra tu contador".

**9. Personal:** staff directory with document completeness per person, sitio asignado, estado; the converted candidate from the signature flow appears here.

## Constraints

- Single self-contained page, desktop-first (min 1280px), in-memory state only (no localStorage).
- Prioritize believability of the demo path over completeness: dashboard → Kanban → perfil → convertir a personal → calendario → asistencia → incidencias → pre-nómina → switch to Invitado role.
- Buttons that would need a real backend show a polite toast ("disponible en la versión completa") instead of doing nothing.
