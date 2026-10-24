output "api_url" {
  value = google_cloud_run_v2_service.api.uri
}

output "db_private_ip" {
  description = "Host of DATABASE_URL and MIGRATIONS_DATABASE_URL."
  value       = google_sql_database_instance.platform.private_ip_address
}

output "image_repository" {
  value = "${var.region}-docker.pkg.dev/${var.project_id}/platform"
}

output "hosting_sites" {
  value = { for k, s in google_firebase_hosting_site.apps : k => s.default_url }
}

output "manual_secrets" {
  description = "Secrets that need a version added by hand before the first API deploy."
  value       = sort([for s in google_secret_manager_secret.manual : s.secret_id])
}
