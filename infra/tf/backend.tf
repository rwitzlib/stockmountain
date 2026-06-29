terraform {
  required_version = ">= 1.5"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 6.0.0 < 7.0.0"
    }
    restapi = {
      source  = "Mastercard/restapi"
      version = "~> 2.0"
    }
  }

  backend "s3" {}
}
