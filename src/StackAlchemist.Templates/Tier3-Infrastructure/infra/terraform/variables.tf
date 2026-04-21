variable "app_url" {
  description = "Public application URL."
  type        = string
  default     = "https://example.com"
}

variable "aws_region" {
  description = "AWS region to deploy into."
  type        = string
  default     = "us-east-1"
}

variable "container_port" {
  description = "Port exposed by the application container."
  type        = number
  default     = 8080
}

variable "database_instance_class" {
  description = "RDS instance class."
  type        = string
  default     = "db.t4g.micro"
}

variable "database_password" {
  description = "Initial PostgreSQL password. Prefer setting through a secret manager."
  sensitive   = true
  type        = string
}

variable "database_username" {
  description = "Initial PostgreSQL username."
  type        = string
  default     = "{{ProjectNameLower}}"
}

variable "health_check_path" {
  description = "HTTP health check path."
  type        = string
  default     = "/healthz"
}

variable "image_uri" {
  description = "Container image URI to deploy."
  type        = string
}

variable "project_name" {
  description = "DNS-safe project name used for resource naming."
  type        = string
  default     = "{{ProjectNameKebab}}"
}
