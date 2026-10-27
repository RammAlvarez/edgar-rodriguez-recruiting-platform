# Terraform — Google Cloud

Everything in [`deploy/providers/gcp.md`](../../providers/gcp.md) that is a resource: APIs, Artifact Registry, Cloud SQL
(private IP), the two buckets + HMAC key, Secret Manager secrets, the migrate job, the API service, Cloud Scheduler jobs
and the three Firebase Hosting sites. Images, `db/roles.sql`, secret values, domains and the HANDOFF after-steps stay manual.

Differences from the gcloud guide:
- **Database on its private IP** through Direct VPC egress (Cloud Run has no fixed egress IP for authorized networks).
- **No `platform-scheduler` service:** the crontab runs as Cloud Scheduler jobs calling `/internal/jobs/*` (`scheduler.tf`;
  keep it in step with `services/scheduler/crontab`). The image stays for the same-VM and DigitalOcean variants.
- **Secrets from Secret Manager**, still read by the API as plain environment variables.
- `min_instance_count` is fixed (`api_min_instances`, default 1); gcp.md's "business hours only" is not expressed here.

## Environments

One project per environment, each with its own state bucket and `ENV.tfvars` (`dev`, `prod`); pass `-var-file=ENV.tfvars` to every
`plan`/`apply`, and `init -reconfigure` with that environment's bucket when switching.
`ASPNETCORE_ENVIRONMENT` stays `Production` in every cloud environment (`Development` accepts insecure defaults).

## First time

If the project already has Firebase (the usual case: Auth lives there), import it once after `init`:
`terraform import google_firebase_project.platform projects/PROJECT`.

```sh
gcloud auth application-default login
gcloud storage buckets create gs://PROJECT-tfstate --location=us-central1 --uniform-bucket-level-access
gcloud storage buckets update gs://PROJECT-tfstate --versioning
cp dev.tfvars.example dev.tfvars                      # or prod; one Google Cloud project and state bucket per environment
export TF_VAR_db_postgres_password='…'
terraform init -reconfigure -backend-config="bucket=PROJECT-tfstate"
```

1. `terraform apply -var-file=ENV.tfvars -target=google_artifact_registry_repository.platform -target=google_sql_database.platform -target=google_secret_manager_secret.manual`
2. Build and push the api image (gcp.md §4) with the tag in `image_tag`.
3. Run `db/roles.sql` as `postgres` (gcp.md §2; the instance has no public IP, so from a VM in the `platform` network or
   `gcloud sql connect` after temporarily enabling the public IP).
4. Add a version to each secret in `terraform output manual_secrets` except `encryption-previous-keys` (`terraform output db_private_ip` is the URLs' host):
   `printf %s '…' | gcloud secrets versions add database-url --data-file=-`
5. `terraform apply -var-file=ENV.tfvars`, then `gcloud run jobs execute platform-migrate --region us-central1 --wait`.
6. Apps: gcp.md §6 (`firebase.json` targets → the site ids in `hosting_site_ids`), custom domains, Auth → Authorized domains.
7. HANDOFF §3 steps 7–10.

## Every upgrade

Push the new image, then `terraform apply -var-file=ENV.tfvars -var=image_tag=<tag> -target=google_cloud_run_v2_job.migrate`,
`gcloud run jobs execute platform-migrate --region us-central1 --wait`, and `terraform apply -var-file=ENV.tfvars -var=image_tag=<tag>`
(keep the new tag in `ENV.tfvars`).
Key rotation (HANDOFF §6): add the new key as a version of `encryption-key` and the old one to
`encryption-previous-keys`, then apply `encryption_key_version`, `encryption_key_id` and `encryption_previous_keys_version`
together; the second deploy sets `encryption_previous_keys_version = ""`.
Any other changed secret value takes effect on the next revision: `gcloud run services update platform-api --region us-central1`.

`terraform fmt -check -recursive && terraform validate` runs in CI.
