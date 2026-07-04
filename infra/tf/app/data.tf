data "aws_ssm_parameter" "polygon_token" {
  name = "/tokens/polygon"
}

data "aws_ssm_parameter" "deploy_token" {
  name = "/tokens/${var.environment}/deploy"
}

data "aws_kms_key" "lambda" {
  key_id = "alias/aws/lambda"
}

data "aws_ecr_image" "market_data_aggregator" {
  repository_name = "${var.team}-${var.environment}-${local.market_data_aggregator_service}"
  image_tag       = var.image_tag
}

data "aws_ecr_image" "backtester" {
  repository_name = "${var.team}-${var.environment}-backtester"
  image_tag       = var.image_tag
}

# data "aws_ecr_image" "kesha" {
#   repository_name = "${local.kesha_team}-${var.environment}-${local.kesha_business_domain}-${local.kesha_service}"
#   image_tag       = var.image_tag
# }

# data "aws_ecr_image" "web_app" {
#   repository_name = "${var.team}-${var.environment}-${local.web_service_name}"
#   image_tag       = var.image_tag
# }
