# 2026-10-16-03 — S4 Reclutamiento

- **Build:** `06870e1` (S4 step 6) plus the fixes of this run
- **Environment:** local (TEST_PLAN §2) — Postgres 18 on :5434, MinIO, Auth emulator :9199, API :5080 (S4 build), admin :4300; Chrome desktop 1568 px; 390 px through a same-origin iframe (the window manager refuses to shrink the window)
- **Data:** `npm run seed` (13 vacantes, 32 candidatos, two notes); second run «vacantes: already there, skipped». Recruiting tables were emptied once and reseeded after D-006.

| # | Step (TEST_PLAN §3 S4) | Expected | Result |
|---|---|---|---|
| 1 | Vacantes | 13 seed vacantes; HEB's two CAJERO carry the date; GTS *Cubierta*, NM FIRE *Pausada*, CONSTRUCTORA HFC *Cancelada* | pass |
| 2 | «Nueva vacante» HEB, Auxiliar de limpieza, 3 | toast «Vacante creada», opens «HEB – AUXILIAR DE LIMPIEZA», *Abierta* | pass |
| 3 | «Nuevo candidato» from the vacante (new person) | Vacante preset with «Solicitadas 3 · Contratadas 0»; card in Nuevo; vacante *En proceso* | pass |
| 4 | Kanban drag Nuevo → Apto | card moves, toast «… → Apto» | pass |
| 5 | Candidate detail | Datos personales, checklist, notes newest first | pass |
| 6 | Note with H2 + bulleted list | toolbar works; saved note on top, rendered | pass |
| 7 | Upload CURP from the browser, Validar | presigned PUT to MinIO, *Entregado* → *Validado*, completeness 0 % → 16 % | pass (first browser upload of the project) |
| 8 | Reclutadores | 6 cards with live KPIs | pass |
| 9 | Mónica → «Asignar vacante» HEB – Auxiliar de piso as Líder | note «Pricila de León pasará a Apoyo»; row in her table | pass |
| 10 | «Ver como reclutador» | recruiter sidebar, banner, her 5 vacantes | pass |
| 11 | Covering: Contactado → Entrevista | Historial «Edgar Rodríguez (cubriendo a Mónica Soto)» | pass |
| 12 | «Salir» | her detail, full sidebar | pass |
| 13 | Sign in as monica.soto@local.test | Mis vacantes / Mis candidatos only, her data only | pass |
| 14 | As Reclutadora, type /reclutadores | back to Mis vacantes | **fail → D-007, fixed, pass** |
| 15 | 390 px (carried from S0, S3) | no page-level horizontal scroll; tables scroll in their card; candidate detail one column | pass (Mis vacantes, vacante detail, Mis candidatos, candidate detail: `scrollWidth` 390) |

Observations fixed in the run: Actividad logged «Asignada … como apoyo» and «… como líder» for one líder assignment (D-008); the «$» prefix sat on top of the empty Sueldo label; a Zona hint touched the next field's floated label (drawer fields now 10 px apart). The first click right after a full page load sometimes did nothing in the automated browser; with a capture listener the click arrived and navigated — an automation artifact, not reproduced by hand. No console errors.
