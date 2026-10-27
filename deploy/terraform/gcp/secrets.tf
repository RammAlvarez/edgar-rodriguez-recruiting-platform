# The API still reads plain environment variables (ARCHITECTURE §5 "Secrets"); Cloud Run fills them from these secrets.
# Values Terraform cannot know are added by hand, so they never enter the state:
#   printf %s '<value>' | gcloud secrets versions add <name> --data-file=-
locals {
  manual_secrets = toset([
    "DATABASE_URL",             # postgres://platform_api:…@<db_private_ip>:5432/platform?sslmode=require
    "MIGRATIONS_DATABASE_URL",  # same with platform_owner
    "ENCRYPTION_KEY",           # openssl rand -base64 32, backed up apart from the database (HANDOFF §6)
    "ENCRYPTION_PREVIOUS_KEYS", # id:base64,id:base64, only during a rotation
    "FIREBASE_SERVICE_ACCOUNT_JSON",
    "RESEND_API_KEY",
  ])
  generated_secrets = toset(["INTERNAL_SECRET", "S3_ACCESS_KEY_ID", "S3_SECRET_ACCESS_KEY"])
}

resource "google_secret_manager_secret" "manual" {
  for_each  = local.manual_secrets
  secret_id = local.secret_ids[each.value]
  replication {
    auto {}
  }
  depends_on = [google_project_service.this]
}

# Known to Terraform anyway (the HMAC secret is in its state; the scheduler jobs send INTERNAL_SECRET), so it writes them.
resource "random_password" "internal_secret" {
  length  = 48
  special = false
}

resource "google_secret_manager_secret" "generated" {
  for_each  = local.generated_secrets
  secret_id = local.secret_ids[each.value]
  replication {
    auto {}
  }
  depends_on = [google_project_service.this]
}

resource "google_secret_manager_secret_version" "generated" {
  for_each = {
    INTERNAL_SECRET      = random_password.internal_secret.result
    S3_ACCESS_KEY_ID     = google_storage_hmac_key.storage.access_id
    S3_SECRET_ACCESS_KEY = google_storage_hmac_key.storage.secret
  }
  secret      = google_secret_manager_secret.generated[each.key].id
  secret_data = each.value
}

# Built from the names, not the resources, so for_each keys are known before the secrets exist. Cloud Run still waits
# for the secrets through google_secret_manager_secret_iam_member.api, which references them.
locals {
  secret_ids = { for name in setunion(local.manual_secrets, local.generated_secrets) : name => replace(lower(name), "_", "-") }
}
