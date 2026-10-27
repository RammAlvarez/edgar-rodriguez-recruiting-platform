variable "project_id" {
  description = "Google Cloud project, the same one as Firebase Auth."
  type        = string
}

variable "region" {
  type    = string
  default = "us-central1"
}

variable "image_tag" {
  description = "Tag of the api image in Artifact Registry (e.g. 2026-10-17). The migrate job and the API always share it."
  type        = string
}

variable "db_tier" {
  description = "db-g1-small (gcp.md) or db-f1-micro (cheapest, no SLA; ARCHITECTURE §6)."
  type        = string
  default     = "db-g1-small"
}

variable "db_deletion_protection" {
  description = "Off only in dev, so the environment can be torn down."
  type        = bool
  default     = true
}

variable "db_postgres_password" {
  description = "Password of the instance's postgres user, used once to run db/roles.sql."
  type        = string
  sensitive   = true
}

variable "admin_app_url" {
  type = string
}

variable "candidates_app_url" {
  type = string
}

variable "clients_app_url" {
  type = string
}

variable "email_from" {
  type = string
}

variable "trusted_proxy_hops" {
  description = "1 for the run.app URL or a domain mapping, 2 behind a load balancer (HANDOFF §9)."
  type        = number
  default     = 1
}

variable "api_min_instances" {
  type    = number
  default = 1
}

# Key rotation (HANDOFF §6) changes these together in one apply, so key and id always reach the same revision.
variable "encryption_key_id" {
  type    = string
  default = "1"
}

variable "encryption_key_version" {
  description = "Version of the encryption-key secret the API runs with (pinned, never latest)."
  type        = string
  default     = "1"
}

variable "encryption_previous_keys_version" {
  description = "Version of the encryption-previous-keys secret, only during a rotation; empty leaves the variable out."
  type        = string
  default     = ""
}

variable "hosting_site_ids" {
  description = "Firebase Hosting site ids (globally unique), keyed by the firebase.json target."
  type        = map(string)
}
