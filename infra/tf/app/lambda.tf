resource "aws_lambda_function" "market_data_aggregator" {
  function_name = "${var.team}-${var.environment}-${local.market_data_aggregator_service}"
  role          = aws_iam_role.market_data_aggregator_lambda.arn

  memory_size = 4096
  timeout     = 120

  architectures = ["x86_64"]

  package_type = "Image"
  image_uri    = data.aws_ecr_image.market_data_aggregator.image_uri

  image_config {
    command = ["MarketDataAggregator::MarketDataAggregator.AggregatorFunction::FunctionHandler"]
  }

  environment {
    variables = {
      POLYGON_TOKEN                  = data.aws_ssm_parameter.polygon_token.value
      ASPNETCORE_ENVIRONMENT         = var.environment
      BATCH_SIZE                     = 850
      MARKET_DATA_BUCKET_NAME        = aws_s3_bucket.market_data.bucket
      MARKET_DATA_CATALOG_TABLE_NAME = aws_dynamodb_table.market_data.name
    }
  }

  kms_key_arn = data.aws_kms_key.lambda.arn
}

resource "aws_lambda_function" "market_data_orchestrator" {
  function_name = "${var.team}-${var.environment}-${local.market_data_orchestrator_service}"
  role          = aws_iam_role.market_data_aggregator_lambda.arn

  memory_size = 1024
  timeout     = 1200

  architectures = ["x86_64"]

  package_type = "Image"
  image_uri    = data.aws_ecr_image.market_data_aggregator.image_uri

  image_config {
    command = ["MarketDataAggregator::MarketDataAggregator.OrchestratorFunction::FunctionHandler"]
  }

  environment {
    variables = {
      ASPNETCORE_ENVIRONMENT               = var.environment
      MARKET_DATA_CATALOG_TABLE_NAME       = aws_dynamodb_table.market_data.name
      MARKET_DATA_AGGREGATOR_FUNCTION_NAME = aws_lambda_function.market_data_aggregator.function_name
    }
  }

  kms_key_arn = data.aws_kms_key.lambda.arn
}

resource "aws_lambda_permission" "market_data_aggregator_cloudwatch" {
  statement_id  = "AllowExecutionFromCloudWatch"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.market_data_aggregator.function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.market_data_aggregator_daily.arn
}