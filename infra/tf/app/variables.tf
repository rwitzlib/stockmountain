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

variable "web_port" {
  type        = string
  default     = "5173"
  description = "Port for the web service to listen on."
}

variable "api_port" {
  type        = string
  default     = "8080"
  description = "Port for the API service to listen on."
}

variable "api_url" {
  type        = string
  default     = "https://dev.stockmountain.io"
  description = "URL for the API service."
}

variable "clerk_webhook_signing_secret" {
  type        = string
  default     = ""
  sensitive   = true
  description = "Secret for signing Clerk webhooks."
}

variable "otel_exporter_otlp_headers" {
  type        = string
  default     = ""
  description = "Headers for the OTLP exporter."
}

variable "otel_exporter_otlp_endpoint" {
  type        = string
  default     = ""
  description = "Endpoint for the OTLP exporter."
}

variable "otel_exporter_otlp_protocol" {
  type        = string
  default     = ""
  description = "Protocol for the OTLP exporter."
}

variable "otel_resource_attributes" {
  type        = string
  default     = ""
  description = "Attributes for the OTLP resource."
}

variable "massive_token" {
  type        = string
  default     = ""
  sensitive   = true
  description = "Massive API token."
}

variable "alpaca_api_key_id" {
  type        = string
  default     = ""
  sensitive   = true
  description = "Alpaca API key ID."
}

variable "alpaca_api_secret_key" {
  type        = string
  default     = ""
  sensitive   = true
  description = "Alpaca API secret key."
}