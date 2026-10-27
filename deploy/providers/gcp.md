# Google Cloud (first provider)

| Piece | Service |
|---|---|
| Postgres | Cloud SQL for PostgreSQL 18, smallest shared-core tier, automatic backups + point-in-time recovery |
| API | Cloud Run service `platform-api` (min 1 instance during business hours, max 3) |
| Migrations | Cloud Run **job** `platform-migrate`, same image, command `./efbundle` |
| Scheduler | Cloud Scheduler, one HTTP job per line of `services/scheduler/crontab` (the crond image has no HTTP port for Cloud Run) |
| Apps | Firebase Hosting, three sites (admin, candidatos, clientes) |
| Storage | Cloud Storage, two buckets, through the S3-interoperability endpoint with an HMAC key |
| Identity | Firebase Auth of the same project |

> **Terraform:** [`deploy/terraform/gcp`](../terraform/gcp/README.md) creates §1–3, §5 and the Hosting sites of §6 (database on its
> private IP, the scheduler as Cloud Scheduler jobs). The steps below are the same setup by hand.

All commands are run by a person with `gcloud` signed in to the project. Region: `us-central1` (or the closest one).

## 1. Project and registry

```sh
gcloud config set project PROJECT_ID
gcloud services enable run.googleapis.com sqladmin.googleapis.com artifactregistry.googleapis.com
gcloud artifacts repositories create platform --repository-format=docker --location=us-central1
gcloud auth configure-docker us-central1-docker.pkg.dev
```

## 2. Database

```sh
gcloud sql instances create platform-db --database-version=POSTGRES_18 --tier=db-g1-small --region=us-central1 \
  --backup --enable-point-in-time-recovery --availability-type=zonal
gcloud sql databases create platform --instance=platform-db
gcloud sql users set-password postgres --instance=platform-db --password='…'
```
Run `db/roles.sql` once (from Cloud Shell: `gcloud sql connect platform-db --user=postgres --database=platform`, then
`\i db/roles.sql` with the three `-v …_password` variables set by `\set`). Connect the API over TCP with SSL: the simplest is
the instance's public IP with authorized networks limited to Cloud Run's egress (or the Cloud SQL connector's socket path).
`DATABASE_URL=postgres://platform_api:…@<ip>:5432/platform?sslmode=require`.

## 3. Storage

Two buckets, uniform access, no public access:
```sh
gcloud storage buckets create gs://PROJECT-documents --location=us-central1 --uniform-bucket-level-access
gcloud storage buckets create gs://PROJECT-exports --location=us-central1 --uniform-bucket-level-access
gcloud storage buckets update gs://PROJECT-documents --versioning
```
Lifecycle on exports (delete after 90 days) and CORS on documents for the app origins (`PUT`, `GET`, `Content-Type`). Create a
service account `platform-storage` with Storage Object Admin on both buckets and an **HMAC key** for it:
`S3_ENDPOINT=https://storage.googleapis.com`, `S3_REGION=auto`, `S3_FORCE_PATH_STYLE=true`, the HMAC id and secret as the
access key.

## 4. Images

```sh
docker build -f services/platform-api/Dockerfile -t us-central1-docker.pkg.dev/PROJECT/platform/api:2026-10-17 .
docker push …/api:2026-10-17
```
(On an Apple-silicon laptop add `--platform linux/amd64`; the Dockerfile cross-compiles, so the SDK never runs emulated.)

## 5. Migrations, API, scheduler

Put the variables of `.env.example` in an env file (`api.env.yaml`, never committed) and:
```sh
gcloud run jobs create platform-migrate --image …/api:2026-10-17 --region us-central1 --command ./efbundle \
  --set-env-vars MIGRATIONS_DATABASE_URL='postgres://platform_owner:…@<ip>:5432/platform?sslmode=require'
gcloud run jobs execute platform-migrate --region us-central1 --wait
gcloud run deploy platform-api --image …/api:2026-10-17 --region us-central1 --env-vars-file api.env.yaml \
  --min-instances 1 --max-instances 3 --allow-unauthenticated --port 8080
for job in attendance-lock:'*/15 * * * *' period-roll:'0 8 * * *' status-roll:'5 8 * * *' \
    document-expiry:'0 9 * * *' notifications-digest:'0 14 * * *'; do
  gcloud scheduler jobs create http "platform-${job%%:*}" --location us-central1 --schedule "${job#*:}" --time-zone Etc/UTC \
    --http-method POST --uri "https://platform-api-….run.app/internal/jobs/${job%%:*}" \
    --headers X-Internal-Secret=… --attempt-deadline 300s --max-retry-attempts 3
done
```
The API's `/internal/*` stays on its public URL protected by `INTERNAL_SECRET` (≥ 32 characters, compared in constant time);
for a tighter setup, put the API behind a load balancer with a rule that blocks `/internal`, and let the scheduler call the
Cloud Run URL directly. Map `api.<domain>` with `gcloud run domain-mappings create` (or the load balancer).
Put `TRUSTED_PROXY_HOPS=1` in `api.env.yaml` for the `run.app` URL or a domain mapping, `2` behind a load balancer, and
check it once (HANDOFF §9: `ClientIp` in the request log is the caller's own address).

Every upgrade: build and push, `gcloud run jobs update platform-migrate --image …` + `execute --wait`, then
`gcloud run deploy platform-api --image …`.

## 6. Apps on Firebase Hosting

`firebase.json` with three targets (`admin`, `candidatos`, `clientes`), each `public` = `dist/<app>/browser` and a rewrite of
`**` to `/index.html`. Build with `npx ng build admin` (etc.), **replace** `dist/<app>/browser/config.json` with the
environment's (`deploy/config/*.json.example`), then `firebase deploy --only hosting`. `scripts/deploy-hosting.sh <alias>` does the build, the `config.json` swap and the deploy (targets in `.firebaserc`;
the Terraform output `app_config` is the file's body). Connect the three custom domains in
Hosting and add them to Auth → Authorized domains and to `APP_ORIGINS`.

## 7. After

HANDOFF §3 steps 7–10 (health, owner, import, encryption check), uptime check on `/health`, and a calendar entry for the
quarterly restore drill (HANDOFF §7: restore Cloud SQL to a new instance, point one Cloud Run revision without traffic at it).
