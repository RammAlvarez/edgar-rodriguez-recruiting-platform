# 2026-10-16-02 — S3 Core registry

- **Build:** `d0b29d1` (S3 step 5) plus the outlined-fields fix of this run
- **Environment:** local (TEST_PLAN §2) — Postgres 18 on :5434, MinIO, Auth emulator :9199, API :5080 (restarted on the S3 build), admin :4300; Chrome desktop 1568 px
- **Data:** `npm run seed` twice (second run: «0 created, 11 already there», no duplicate recruiters)

| # | Step (TEST_PLAN §3 S3) | Expected | Result |
|---|---|---|---|
| 1 | Usuarios | owner «tú» with last access, 6 recruiters pending | pass |
| 2 | Invitar usuario (Administrador) | defaults without Reportes; toast; row appears; email in the API log | pass |
| 3 | Clientes → ECOVIA | 11 clients with zona and contact; detail tabs | pass |
| 4 | Invitar al portal | «Con acceso al portal», no delete; Cliente user in Usuarios | pass |
| 5 | Nuevo cliente «ecovia» | 409 message in the panel | pass |
| 6 | Clientes (nómina) | empty active table, all clients «Sin proyecto» | pass |
| 7 | Catálogos | tabs, 51 municipios | pass (holiday button checked by the seed and `CatalogTests`) |
| 8 | Document upload/download | API-level (no people screen in S3) | pass — `DocumentTests` 9, `S3ObjectStorageTests` 2 |
| 9 | 390 px | sidebar collapses | **not run** — the browser window would not resize below its minimum width; carried to S4 |

Observation fixed in the run: form fields rendered with the *fill* appearance; the design system asks for *outline* → `providePlatformMaterial()` (libs/ui) in the three apps. No console errors.
