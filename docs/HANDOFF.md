# HANDOFF — operating the Plataforma de Reclutamiento y Prenómina

> For whoever installs, runs and recovers the platform. Architecture: [ARCHITECTURE.md](./ARCHITECTURE.md); decisions:
> [PLAN.md](./PLAN.md). Provider guides: [`deploy/providers/gcp.md`](../deploy/providers/gcp.md),
> [`deploy/providers/digitalocean.md`](../deploy/providers/digitalocean.md), [`deploy/providers/same-vm.md`](../deploy/providers/same-vm.md).
> No agent runs cloud commands: every command below is run by a person.

## 1. What runs

| Piece | Image / source | Notes |
|---|---|---|
| API | `services/platform-api/Dockerfile` → `platform-api` | ASP.NET on :8080; also carries `./efbundle`, the database migrations |
| Migrations | same image, entrypoint `./efbundle` | run **before** a new API version takes traffic; needs `MIGRATIONS_DATABASE_URL` (owner role) |
| Scheduler | `services/scheduler` | cron calling `POST /internal/jobs/{name}` with `X-Internal-Secret` (crontab in that folder, UTC) |
| Apps | `deploy/apps.Dockerfile` (Caddy, same VM) or any static host | admin, candidatos, clientes; each reads `/config.json` at start |
| Postgres 18 | managed, or the same-VM container | three roles from `db/roles.sql`: `platform_owner` (migrations), `platform_api` (the API), `platform_readonly` |
| Storage | an S3-compatible bucket pair | `documents` (private, versioning on) and `exports` (private, lifecycle 90 days) |
| Identity | Firebase Auth project | email/password (staff), email link (candidates, clients); two-step verification is the platform's own (PLAN D89) |
| Email | Resend | domain verified (SPF/DKIM) for `EMAIL_FROM` |

Jobs (crontab, UTC): `attendance-lock` every 15 min, `period-roll` 08:00, `status-roll` 08:05, `document-expiry` 09:00,
`notifications-digest` 14:00. All are idempotent: running one again is always safe.

## 2. Environment variables

The API reads only environment variables (D34); the full list with comments is the repository's `.env.example`. Outside
Development every one is required. The ones that need care:

| Variable | Care |
|---|---|
| `ENCRYPTION_KEY` | base64 of 32 random bytes (`openssl rand -base64 32`). Encrypts account and card numbers and the two-step-verification secrets. **Without it nothing encrypted can be read.** See §6. |
| `ENCRYPTION_KEY_ID` | 1 today; written in front of every encrypted value (D82) so a later key can coexist. |
| `INTERNAL_SECRET` | ≥ 32 characters; shared only by the scheduler and the operator. `/internal/*` must not be reachable from the internet where possible (Caddy answers 404 for it on the same VM). |
| `DATABASE_URL` / `MIGRATIONS_DATABASE_URL` | `platform_api` for the API, `platform_owner` for migrations — never the superuser. `?sslmode=require` on managed databases. |
| `FIREBASE_SERVICE_ACCOUNT_JSON` | one line; lets the API create users and password links. |
| `APP_ORIGINS` | the three app URLs exactly; nothing else gets CORS. |
| `RATE_LIMIT_*`, `TRUSTED_PROXY_HOPS` | request limits (PLAN D92); the defaults fit 500 employees. `TRUSTED_PROXY_HOPS` must match the proxies in front of the API (1 for Caddy, Cloud Run, App Platform; 2 behind a load balancer), or every request counts against the proxy's address — or a forged header picks its own. |

The apps' `/config.json` (`deploy/config/*.json.example`): `apiUrl` and the Firebase **web** config (apiKey, authDomain,
projectId). It is public by nature; it changes per environment without rebuilding.

## 3. First installation

1. **Database:** create the database `platform`, then as the provider's admin user
   `psql "$ADMIN_DATABASE_URL" -v owner_password=… -v api_password=… -v readonly_password=… -f db/roles.sql`.
   (The same-VM compose does this by itself on its first start.)
2. **Buckets:** two private buckets; versioning on `documents`; a 90-day expiry rule on `exports`; an access key limited to
   both. CORS on `documents` for the candidates and admin origins (`PUT`, `GET`, header `Content-Type`), because uploads go
   straight from the browser with presigned URLs.
3. **Firebase:** a project per environment (`-staging`, `-prod`). Authentication → Sign-in method: *Email/Password* with
   *Email link* enabled. Settings → Authorized domains: the three app domains. A **web app** registered (its config goes in
   the three `config.json`). Project settings → Service accounts → generate a key (→ `FIREBASE_SERVICE_ACCOUNT_JSON`).
   Templates (Spanish): password reset and email link.
4. **Resend:** add and verify the sending domain; create an API key (→ `RESEND_API_KEY`).
5. **Secrets:** generate `ENCRYPTION_KEY` and `INTERNAL_SECRET`; store `ENCRYPTION_KEY` in the password manager **and** in
   the offline copy of §6 before going further.
6. **Deploy** following the provider guide: migrations, then the API, the scheduler and the apps.
7. **Check:** `GET https://api…/health` → `{"status":"ok","database":"ok","bucket":"ok"}`; the apps open.
8. **Owner:** `API_URL=… INTERNAL_SECRET=… node scripts/bootstrap.mjs --email dueno@empresa.mx --name "Edgar Rodríguez"`
   prints the link to set the password. The owner then turns on **Verificación en dos pasos** (account menu) and can require it
   for every Administrador in Usuarios.
9. **Import** (once, on the empty installation, PLAN D90). Put the client's six workbooks in one folder under these names, or
   pass each by path with its option:

   | Option | Sample name |
   |---|---|
   | `--recruiting` | `Sistema_Reclutamiento_Completo_2026.xlsx` |
   | `--sem38` | `PRENOMINA SEM 38 12 HRS ECOVIA VALIDADORES.xlsx` |
   | `--banorte` | `Alta_Nomina_Masiva Banorte 21Ago-1.xlsx` |
   | `--banregio` | `ALTA BANREGIO EARL 19AGO26.xlsx` |
   | `--master` | `Lo HFC MAESTRO 14AGOSTO.xlsx` |
   | `--frequent` | `cuentasfrec21sept.xlsx` |

   Take the owner's ID token with the REST sign-in (the web `apiKey` of `config.json`; the token lasts one hour):
   ```sh
   curl -s -X POST "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=<web apiKey>" \
     -H 'Content-Type: application/json' \
     -d '{"email":"dueno@empresa.mx","password":"…","returnSecureToken":true}' | jq -r .idToken
   ```
   then, with the current code of the owner's authenticator if two-step verification is on,
   `API_URL=… IMPORT_TOKEN=… [IMPORT_MFA_CODE=123456] node scripts/import-excel.mjs --dir ./excel --out ./importacion.md`.
   **Before anyone generates an alta file:** the import queues a Banorte card for everyone without an account in COMENTARIOS
   (34 of the 115 in the sample; `--card-bank` picks the bank). Capture in Personal the accounts the payroll clerk already
   knows for those people first, or the cards are requested for nothing.
   The report lists what was loaded, Semana 38 against V121 (the only expected differences are the festivo primera mitad
   rows of PLAN D43 / SPEC Q12) and every row left for manual review. It contains names: keep it private.
10. **Encryption check:** `curl -H "X-Internal-Secret: $INTERNAL_SECRET" https://api…/internal/health/encryption` → `ok`
    once an account exists.

## 4. Upgrades

1. Back up (§5) — a managed database's automatic backup of the last hours counts.
2. Build the new images (`docker build -f services/platform-api/Dockerfile -t platform-api:<date> .`, and the apps).
3. Run the migrations with the new image (`./efbundle`); they are forward-only and run inside a lock, so a second run waits.
4. Roll out the API, then the apps (the apps tolerate the previous API for the few seconds in between).
5. `GET /health`; open Prenómina and one report.

## 5. Backups

- **Managed Postgres:** daily backups with point-in-time recovery, retention ≥ 7 days (both providers' smallest tiers
  offer it). Keep the provider's default.
- **Same VM:** `docker compose … exec postgres pg_dump -U postgres -d platform -Fc -f /backups/platform-$(date +%F).dump`
  daily from the host's cron, then copy `deploy/backups/` off the VM (another provider's bucket or the owner's drive) and
  keep 30 days.
- **Buckets:** versioning on `documents`; the provider keeps the objects. Exports are disposable (90 days).
- **The key:** not in any backup of the database — see §6.

## 6. Encryption key («Llave de cifrado»)

The database alone cannot be read without `ENCRYPTION_KEY`, and the key alone means nothing without the database. Keep them
apart:

1. In the owner's password manager, entry «Plataforma — ENCRYPTION_KEY (producción)», with its key id (1).
2. On paper or an offline USB in the owner's safe, the same value and the date it was created.
3. Never in the repository, a chat, an email or a bucket next to the database dumps.

**Never just replace the key.** Swapping `ENCRYPTION_KEY` for another value makes every stored account number, card number
and MFA secret unreadable (the API answers 500 when it needs one, and `/internal/health/encryption` says `failed`). Losing
the key means re-capturing the accounts and asking every staff user to set up their two-step verification again.

### Rotating the key (a leak, a person with access leaving, or on schedule)

Each value carries the id of the key that encrypted it, so the old key keeps reading old values while the new one takes
over:

1. Back up the database (§5).
2. Generate the new key (`openssl rand -base64 32`) and give it the next id (2, then 3…). Store it as in steps 1–2 above
   **before** deploying it.
3. Deploy the API with `ENCRYPTION_KEY=<new>`, `ENCRYPTION_KEY_ID=<new id>` and
   `ENCRYPTION_PREVIOUS_KEYS=<old id>:<old key>` (several as `1:…,2:…`). New values use the new key; old ones still read.
4. Re-encrypt what is stored:
   `curl -X POST -H "X-Internal-Secret: $INTERNAL_SECRET" https://api…/internal/jobs/reencrypt-fields`.
   It goes through every encrypted column (accounts, the cuenta de cargo, the account snapshots of paid lines and payment
   files, card numbers, MFA secrets) in batches and answers per column `reencrypted`, `pending` and `unreadable`. It is
   safe to run again and while people work; run it until `pending` is 0 everywhere.
5. `unreadable` above 0 means values under a key the API was not given: find that key before going on.
6. Deploy again without `ENCRYPTION_PREVIOUS_KEYS`, check `/internal/health/encryption` says `ok` with the new key id, and
   only then mark the old key **retired** in the password manager and the safe. Don't delete it yet: backups taken before
   step 4 are still under it. Delete it only once the last of those backups has expired (§5: 7 days or more on a managed
   database, 30 on the same VM). To restore one of those backups in the meantime, deploy with the old key in
   `ENCRYPTION_PREVIOUS_KEYS` and run `reencrypt-fields` again.

If the key leaked, also rotate the database passwords and the bucket access keys.

## 7. Restore drill (do it before go-live, then every quarter)

Performed on 2026-10-16 with the same-VM compose (run 11): a value encrypted, the dump restored into a scratch database, an
API on the copy answered `ok`, and another key answered `failed`.

1. Take the latest backup (managed: restore it to a **new** instance; same VM: the newest `.dump`).
2. Same VM: `createdb -U postgres -O platform_owner platform_restore` and
   `pg_restore -U postgres -d platform_restore --no-owner --role=platform_owner /backups/<file>.dump`, then
   `GRANT CONNECT ON DATABASE platform_restore TO platform_api`.
3. Start one API container with `DATABASE_URL` pointing at the copy and the production `ENCRYPTION_KEY`, on no public port.
4. From inside the network: `curl -H "X-Internal-Secret: …" http://<that api>:8080/internal/health/encryption` → `ok`, and
   `/health` → `ok`. Count a few rows (employees, last paid period) against production.
5. Remove the copy and the container; write the date and the result in this file.

## 8. Two-step verification (D89)

- A staff user turns it on from the account menu; an Administrador can require it for anyone in Usuarios.
- **Lost phone:** another Administrador with Usuarios opens the user → «Restablecer verificación en dos pasos». The user sets
  it up again at their next sign-in if it is required. Recovery codes (8, single use) are the user's own way back.
- **The owner locked out with nobody else to reset:** from a trusted machine with the owner database role:
  ```sql
  UPDATE platform.users SET mfa_secret_enc = NULL, mfa_enabled_at = NULL, mfa_last_step = NULL, mfa_failed_attempts = 0,
         mfa_locked_until = NULL WHERE email = 'dueno@empresa.mx';
  DELETE FROM platform.mfa_recovery_codes WHERE user_id = (SELECT uid FROM platform.users WHERE email = 'dueno@empresa.mx');
  DELETE FROM platform.mfa_sessions WHERE user_id = (SELECT uid FROM platform.users WHERE email = 'dueno@empresa.mx');
  ```
- Five wrong codes wait 15 minutes.

## 9. Monitoring

- `GET /health` every minute from the provider's uptime check (answers 503 when the database or the bucket fails).
- Logs are structured JSON on stdout (Serilog): filter by `RequestPath`, `userId`, `level=Error`. The scheduler logs one line
  per job with `ok` / `failed`.
- A failed job runs again at its next tick; `attendance-lock` every 15 minutes recovers by itself.
- `/internal/health/encryption` after any change of secrets.
- **After the first deploy, check `TRUSTED_PROXY_HOPS`:** open any app from your phone on mobile data, and find that request's log
  line: its `ClientIp` must be the phone's public address, not the proxy's (a private `10.…`/`172.…` address) nor an address
  you never had.
- `Rate limit hit` warnings (PLAN D92) name the route, the person or `anonymous`, and the address. A few from one person
  are a stuck screen or a script; many `anonymous` from one address are someone guessing. If an office of staff ever gets
  429s together, `TRUSTED_PROXY_HOPS` is probably wrong (everyone counted as the proxy) or raise `RATE_LIMIT_IP_PER_MINUTE`.
- The sign-in emails of the Candidates and Clients apps are sent by Firebase, not by the API, so these limits do not cover
  them; Firebase applies its own per-project quotas (Authentication → Usage).

## 10. Who to call

| Topic | Where |
|---|---|
| Payroll rules | PLAN D41–D43, D84, D86; SPEC §5.5 |
| Bank files | PLAN D64, D85, D86; SPEC D33, D34, Q15, Q16 |
| Open questions with the client | PLAN §3, SPEC §10 |

## 11. Known issues (to fix)

- **Admin app: no message when two-step verification becomes required for yourself** (found on the GCP dev deploy,
  2026-10-27). An Administrador who switches on «requerir verificación en dos pasos» for their own account in Usuarios gets
  the save accepted (200), and from then on every call except `/v1/me` and `/v1/me/mfa` answers 403 with problem type
  `https://platform.local/problems/mfa` and `MustEnroll` ("Tu cuenta requiere verificación en dos pasos: actívala para
  continuar.", `Platform.Core/Users/Mfa.cs`). The API side is as designed (D89, `AuthenticationMiddleware`); the app shows
  nothing, so the screens just fail. Fix in the app: on that problem type, send the user to the account menu's
  «Verificación en dos pasos» setup (or `MustVerify` → the code prompt) with the API's message; and either warn before
  requiring it for yourself in Usuarios or turn the switch into "set it up now". Workaround: account menu → Verificación en
  dos pasos → activate.
