resource "google_storage_bucket" "documents" {
  name                        = "${var.project_id}-documents"
  location                    = var.region
  uniform_bucket_level_access = true
  public_access_prevention    = "enforced"

  versioning {
    enabled = true
  }

  cors {
    origin          = local.app_origins
    method          = ["PUT", "GET"]
    response_header = ["Content-Type"]
    max_age_seconds = 3600
  }
}

resource "google_storage_bucket" "exports" {
  name                        = "${var.project_id}-exports"
  location                    = var.region
  uniform_bucket_level_access = true
  public_access_prevention    = "enforced"

  lifecycle_rule {
    condition {
      age = 90
    }
    action {
      type = "Delete"
    }
  }
}

# The API talks to the buckets through the S3-interoperability endpoint with this account's HMAC key.
resource "google_service_account" "storage" {
  account_id   = "platform-storage"
  display_name = "Platform storage (HMAC)"
}

resource "google_storage_bucket_iam_member" "storage" {
  for_each = {
    documents = google_storage_bucket.documents.name
    exports   = google_storage_bucket.exports.name
  }
  bucket = each.value
  role   = "roles/storage.objectAdmin"
  member = google_service_account.storage.member
}

resource "google_storage_hmac_key" "storage" {
  service_account_email = google_service_account.storage.email
}
