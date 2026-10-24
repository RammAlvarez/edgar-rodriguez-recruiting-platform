# services/scheduler/crontab as Cloud Scheduler jobs: the scheduler image is a crond with no HTTP port, which Cloud Run
# services cannot host. Same calls (POST /internal/jobs/{name} with X-Internal-Secret), same UTC times; keep both in step.
locals {
  scheduled_jobs = {
    attendance-lock      = "*/15 * * * *"
    period-roll          = "0 8 * * *"
    status-roll          = "5 8 * * *"
    document-expiry      = "0 9 * * *"
    notifications-digest = "0 14 * * *"
  }
}

resource "google_cloud_scheduler_job" "internal" {
  for_each         = local.scheduled_jobs
  name             = "platform-${each.key}"
  region           = var.region
  schedule         = each.value
  time_zone        = "Etc/UTC"
  attempt_deadline = "300s"
  depends_on       = [google_project_service.this]

  retry_config {
    retry_count          = 3
    min_backoff_duration = "10s"
  }

  http_target {
    http_method = "POST"
    uri         = "${google_cloud_run_v2_service.api.uri}/internal/jobs/${each.key}"
    headers = {
      "X-Internal-Secret" = random_password.internal_secret.result
    }
  }
}
