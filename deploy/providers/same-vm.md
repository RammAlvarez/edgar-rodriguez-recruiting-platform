# Same VM (Docker Compose + Caddy)

One Linux VM (2 vCPU, 4 GB RAM, 40 GB disk is plenty at 500 employees) runs Postgres, the migrations, the API, the scheduler
and Caddy, which serves the three apps and the API with automatic HTTPS. Storage is a provider's S3-compatible bucket (PLAN
D54/D91). Tried end to end on a laptop on 2026-10-16 (run 11).

## Steps

1. A VM with Docker Engine and the compose plugin; ports 80 and 443 open, SSH restricted to your IP.
2. DNS: A records for `admin.`, `candidatos.`, `clientes.` and `api.` of your domain → the VM's IP.
3. Buckets, Firebase and Resend as in [HANDOFF §3](../../docs/HANDOFF.md).
4. On the VM: clone the repository (or copy `deploy/`, `db/roles.sql`, `services/` and the app sources), then
   ```sh
   cp deploy/.env.example deploy/.env                 # fill every CHANGE_ME
   for a in admin candidates clients; do cp deploy/config/$a.json.example deploy/config/$a.json; done   # fill them
   docker compose -f deploy/docker-compose.same-vm.yml --env-file deploy/.env up -d --build
   ```
   The first start creates the database and its roles, runs the migrations, then starts the API, the scheduler and Caddy
   (which obtains the certificates).
5. `curl https://api.<domain>/health` → `ok`; `curl -X POST https://api.<domain>/internal/jobs/period-roll` → 404 (blocked).
6. Owner and import: HANDOFF §3 steps 8–10, with `API_URL=http://api:8080` from inside the network
   (`docker compose … exec scheduler sh` has curl), or through `https://api.<domain>` for `/v1`.
7. Backups: the host's cron, HANDOFF §5.

## Upgrades

`git pull && docker compose -f deploy/docker-compose.same-vm.yml --env-file deploy/.env up -d --build` — the `migrate`
service runs first and the API waits for it.

## Trying it on a laptop

Sites `http://admin.localhost` etc., `HTTP_PORT=8088`, `S3_ENDPOINT=http://minio:9000` with `--profile local-s3`, and
`FIREBASE_AUTH_EMULATOR_HOST=host.docker.internal:9199` with `npm run emulators` on the host. Never use the `local-s3`
MinIO for real data: its image is frozen.
