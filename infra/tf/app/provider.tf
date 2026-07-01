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
