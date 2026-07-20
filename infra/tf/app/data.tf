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

data "aws_secretsmanager_secret" "massive_token" {
  name = "${var.team}-${var.environment}-massive-token"
}

data "aws_secretsmanager_secret_version" "massive_token" {
  secret_id = data.aws_secretsmanager_secret.massive_token.id
}