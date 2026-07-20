variable "team" {
  type    = string
  default = "stockmountain"
}

variable "environment" {
  type        = string
  default     = "dev"
  description = "Deployment environment (dev, qa, cert, prod)."
}

variable "region" {
  type    = string
  default = "us-east-2"
}

variable "massive_token" {
  type        = string
  description = "Massive token for the application."
  sensitive   = true
}
