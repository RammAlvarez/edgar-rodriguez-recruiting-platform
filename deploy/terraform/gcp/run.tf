resource "google_service_account" "api" {
  account_id   = "platform-api"
  display_name = "Platform API and migrations (Cloud Run)"
}

resource "google_secret_manager_secret_iam_member" "api" {
  for_each  = local.secret_ids
  secret_id = each.value
  role      = "roles/secretmanager.secretAccessor"
  member    = google_service_account.api.member
}

locals {
  api_env = {
    ASPNETCORE_ENVIRONMENT = "Production"
    APP_ORIGINS            = join(",", local.app_origins)
    ADMIN_APP_URL          = var.admin_app_url
    CANDIDATES_APP_URL     = var.candidates_app_url
    CLIENTS_APP_URL        = var.clients_app_url
    FIREBASE_PROJECT_ID    = var.project_id
    EMAIL_FROM             = var.email_from
    ENCRYPTION_KEY_ID      = var.encryption_key_id
    TRUSTED_PROXY_HOPS     = tostring(var.trusted_proxy_hops)
    S3_ENDPOINT            = "https://storage.googleapis.com"
    S3_REGION              = "auto"
    S3_FORCE_PATH_STYLE    = "true"
    S3_BUCKET_DOCUMENTS    = google_storage_bucket.documents.name
    S3_BUCKET_EXPORTS      = google_storage_bucket.exports.name
    PLATFORM_VERSION       = var.image_tag
  }
  api_secrets = [
    "DATABASE_URL", "FIREBASE_SERVICE_ACCOUNT_JSON", "RESEND_API_KEY",
    "INTERNAL_SECRET", "S3_ACCESS_KEY_ID", "S3_SECRET_ACCESS_KEY",
  ]
}

resource "google_cloud_run_v2_job" "migrate" {
  name                = "platform-migrate"
  location            = var.region
  deletion_protection = false
  depends_on          = [google_secret_manager_secret_iam_member.api]

  template {
    task_count = 1
    template {
      service_account = google_service_account.api.email
      max_retries     = 0
      timeout         = "900s"

      containers {
        image   = local.api_image
        command = ["./efbundle"]
        env {
          name = "MIGRATIONS_DATABASE_URL"
          value_source {
            secret_key_ref {
              secret  = local.secret_ids["MIGRATIONS_DATABASE_URL"]
              version = "latest"
            }
          }
        }
      }

      vpc_access {
        egress = "PRIVATE_RANGES_ONLY"
        network_interfaces {
          network    = google_compute_network.platform.id
          subnetwork = google_compute_subnetwork.run.id
        }
      }
    }
  }
}

resource "google_cloud_run_v2_service" "api" {
  name                = "platform-api"
  location            = var.region
  ingress             = "INGRESS_TRAFFIC_ALL"
  deletion_protection = false
  depends_on          = [google_secret_manager_secret_iam_member.api]

  template {
    service_account = google_service_account.api.email

    scaling {
      min_instance_count = var.api_min_instances
      max_instance_count = 3
    }

    containers {
      image = local.api_image
      ports {
        container_port = 8080
      }

      dynamic "env" {
        for_each = local.api_env
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = toset(local.api_secrets)
        content {
          name = env.value
          value_source {
            secret_key_ref {
              secret  = local.secret_ids[env.value]
              version = "latest"
            }
          }
        }
      }

      env {
        name = "ENCRYPTION_KEY"
        value_source {
          secret_key_ref {
            secret  = local.secret_ids["ENCRYPTION_KEY"]
            version = var.encryption_key_version
          }
        }
      }

      dynamic "env" {
        for_each = var.encryption_previous_keys_version == "" ? [] : [var.encryption_previous_keys_version]
        content {
          name = "ENCRYPTION_PREVIOUS_KEYS"
          value_source {
            secret_key_ref {
              secret  = local.secret_ids["ENCRYPTION_PREVIOUS_KEYS"]
              version = env.value
            }
          }
        }
      }

      startup_probe {
        http_get {
          path = "/health"
        }
        timeout_seconds   = 5
        period_seconds    = 5
        failure_threshold = 12
      }
    }

    vpc_access {
      egress = "PRIVATE_RANGES_ONLY"
      network_interfaces {
        network    = google_compute_network.platform.id
        subnetwork = google_compute_subnetwork.run.id
      }
    }
  }
}

resource "google_cloud_run_v2_service_iam_member" "api_public" {
  name     = google_cloud_run_v2_service.api.name
  location = var.region
  role     = "roles/run.invoker"
  member   = "allUsers"
}
