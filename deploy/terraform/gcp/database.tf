resource "google_sql_database_instance" "platform" {
  name                = "platform-db"
  database_version    = "POSTGRES_18"
  region              = var.region
  deletion_protection = var.db_deletion_protection
  depends_on          = [google_service_networking_connection.sql]

  settings {
    tier              = var.db_tier
    edition           = "ENTERPRISE"
    availability_type = "ZONAL"

    backup_configuration {
      enabled                        = true
      point_in_time_recovery_enabled = true
      transaction_log_retention_days = 7
      backup_retention_settings {
        retained_backups = 7
      }
    }

    ip_configuration {
      ipv4_enabled    = false
      private_network = google_compute_network.platform.id
      ssl_mode        = "ENCRYPTED_ONLY"
    }
  }
}

resource "google_sql_database" "platform" {
  name     = "platform"
  instance = google_sql_database_instance.platform.name
}

# The three platform roles come from db/roles.sql, run once by hand (README); Terraform only sets postgres.
resource "google_sql_user" "postgres" {
  name     = "postgres"
  instance = google_sql_database_instance.platform.name
  password = var.db_postgres_password
}
