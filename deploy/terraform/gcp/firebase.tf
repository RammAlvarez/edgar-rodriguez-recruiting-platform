# Firebase on the same project: Auth stays the plain Firebase Auth (no Identity Platform upgrade, ARCHITECTURE §5).
# Custom domains and Auth → Authorized domains need DNS and stay manual (README).
resource "google_firebase_project" "platform" {
  provider   = google-beta
  project    = var.project_id
  depends_on = [google_project_service.this]
}

resource "google_firebase_hosting_site" "apps" {
  provider = google-beta
  for_each = var.hosting_site_ids
  project  = google_firebase_project.platform.project
  site_id  = each.value
}

# The web config the three apps read from /config.json (deploy/config/*.json); none of it is secret.
resource "google_firebase_web_app" "platform" {
  provider        = google-beta
  project         = google_firebase_project.platform.project
  display_name    = "Platform"
  deletion_policy = "DELETE"
}

data "google_firebase_web_app_config" "platform" {
  provider   = google-beta
  project    = google_firebase_project.platform.project
  web_app_id = google_firebase_web_app.platform.app_id
}
