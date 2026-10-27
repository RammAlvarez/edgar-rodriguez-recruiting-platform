resource "google_project_service" "this" {
  for_each = toset([
    "artifactregistry.googleapis.com",
    "cloudscheduler.googleapis.com",
    "compute.googleapis.com",
    "firebase.googleapis.com",
    "firebasehosting.googleapis.com",
    "run.googleapis.com",
    "secretmanager.googleapis.com",
    "servicenetworking.googleapis.com",
    "sqladmin.googleapis.com",
  ])
  service            = each.value
  disable_on_destroy = false
}

resource "google_artifact_registry_repository" "platform" {
  repository_id = "platform"
  location      = var.region
  format        = "DOCKER"
  depends_on    = [google_project_service.this]
}

locals {
  api_image   = "${var.region}-docker.pkg.dev/${var.project_id}/platform/api:${var.image_tag}"
  app_origins = [var.admin_app_url, var.candidates_app_url, var.clients_app_url]
}
