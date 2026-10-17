# 2026-10-16-10 — S11 Reportes de nómina

- **Build:** `9b66a71` (S11 step 3), which includes this run's fixes
- **Environment:** local (TEST_PLAN §2) — Postgres 18 on :5434, Auth emulator :9199, API :5080 (S11 build), admin :4300; Chrome at 1456 px; the phone through a same-origin iframe of 390 px. For the fresh-database gate, a second API on :5081 against `platform_test` (reset with the truncate + reference-data script).
- **Data:** the owner's local database after the S10 run (Semana 38 paid there) plus `npm run seed`'s S11 block — run before the week-to-week variation was added, so 39–41 there have equal totals; the fresh-database run has them varied.

| # | Step (TEST_PLAN §3 S11) | Expected | Result |
|---|---|---|---|
| — | **Gate (fresh database):** `npm run seed` on `platform_test` through the :5081 API | every block, then «semanas 39–41 … costo de nómina: Semana 38 $334,654.83, Semana 39 $304,867.05, Semana 40 $304,474.18, Semana 41 $304,759.90» | **pass** |
| 1 | Reportes de nómina | Costo de nómina with the last paid Semana (40 locally) | pass |
| 2 | ECOVIA, Rango 11/09 – 08/10 | 38 $334,654.83, 39 $314,157.51 (Δ −$20,497.32, −6 %), 40, 41; total $1,277,127.36; Semana 39's row opens its Prenómina at $314,157.51 | pass |
| 3 | The other five reports | Resumen de pago (Semana 40: total, by bank, 2 pending), Incidencias (none in 40), Asistencia (115 people, «Por día» with F*), Rotación (ene – oct 2026, 2 bajas «Renuncia voluntaria», 17 movements), Tarjetas y cuentas (125 activos, Banregio 1 en espera) | pass — after the fix below |
| 4 | Export step 2 (through the API, read locally) | `Costo de nómina por cliente ECOVIA 2026-09-11 a 2026-10-08.xlsx`: title, filters line, four rows, total 1,277,127.36; the PDF renders | pass |
| 5 | 390 px, each report | page `scrollWidth` 390 | pass |

Fixed in the run: Rotación took the «Últimas 4 semanas» range instead of the year so far (it has its own months now); short cells kept on one line. Found by the API tests: an hours incidencia reported $0 (it stores hours; the amount is now its line items).
