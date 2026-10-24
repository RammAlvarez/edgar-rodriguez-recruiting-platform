# Cloud Run has no fixed egress address, so the database is reached on its private IP through Direct VPC egress
# instead of gcp.md's authorized networks. DATABASE_URL keeps the same shape, with the private IP as the host.
resource "google_compute_network" "platform" {
  name                    = "platform"
  auto_create_subnetworks = false
  depends_on              = [google_project_service.this]
}

resource "google_compute_subnetwork" "run" {
  name                     = "platform-run"
  network                  = google_compute_network.platform.id
  region                   = var.region
  ip_cidr_range            = "10.10.0.0/24"
  private_ip_google_access = true
}

resource "google_compute_global_address" "sql" {
  name          = "platform-sql"
  network       = google_compute_network.platform.id
  purpose       = "VPC_PEERING"
  address_type  = "INTERNAL"
  prefix_length = 20
}

resource "google_service_networking_connection" "sql" {
  network                 = google_compute_network.platform.id
  service                 = "servicenetworking.googleapis.com"
  reserved_peering_ranges = [google_compute_global_address.sql.name]
}
