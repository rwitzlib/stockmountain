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

variable "enable_grafana_cloud_logs" {
  type        = bool
  default     = true
  description = "Forward backtest Lambda CloudWatch logs to Grafana Cloud through lambda-promtail."
}

variable "grafana_loki_write_address" {
  type        = string
  default     = ""
  description = "Grafana Cloud Loki push URL."
}

variable "grafana_loki_username" {
  type        = string
  default     = ""
  description = "Grafana Cloud Logs instance ID used as the Loki basic-auth username."
}

variable "grafana_loki_token" {
  type        = string
  default     = ""
  sensitive   = true
  description = "Grafana Cloud access-policy token with logs:write permission."
}
