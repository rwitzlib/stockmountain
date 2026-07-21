terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 6.0.0, < 7.0.0"
    }
    http = {
      source  = "hashicorp/http"
      version = "~> 3.0"
    }
    restapi = {
      source  = "Mastercard/restapi"
      version = "~> 2.0"
    }
    grafana = {
      source  = "grafana/grafana"
      version = "~> 3.7"
    }
  }

  backend "s3" {}
}

provider "grafana" {
  url  = var.grafana_url != "" ? var.grafana_url : "https://placeholder.grafana.net"
  auth = var.grafana_api_key != "" ? var.grafana_api_key : "placeholder"
}

provider "aws" {
  region = var.region

  default_tags {
    tags = local.default_tags
  }
}

provider "restapi" {
  uri                  = "https://management.stockmountain.io"
  write_returns_object = true
  debug                = true

  headers = {
    "Authorization" = "Bearer ${data.aws_ssm_parameter.deploy_token.value}"
    "Content-Type"  = "application/json"
  }

  create_method = "POST"
}