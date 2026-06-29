data "aws_caller_identity" "current" {}

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

variable "availability_zones" {
  type = list(string)
  default = [
    "us-east-2a"
  ]
}

variable "image_tag" {
  type        = string
  default     = "latest"
  description = "Container image tag for Lambda and deploy targets."
}

variable "deploy_run_id" {
  type        = string
  default     = "local"
  description = "CI run id passed to the management deploy API."
}

variable "deploy_actor" {
  type        = string
  default     = "local"
  description = "Actor name passed to the management deploy API."
}

variable "enable_web_deploy" {
  type        = bool
  default     = false
  description = "When true, triggers a stockmountain-app deploy via the management API."
}
