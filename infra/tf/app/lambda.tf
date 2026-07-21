// ------- Market Data Aggregator Lambda Function -------
resource "aws_lambda_function" "market_data_aggregator" {
  function_name = "${var.team}-${var.environment}-${local.market_data_aggregator_service}"
  role          = aws_iam_role.market_data_aggregator_lambda.arn

  memory_size = 4096
  timeout     = 900

  architectures = ["x86_64"]

  package_type = "Image"
  image_uri    = data.aws_ecr_image.market_data_aggregator.image_uri

  image_config {
    command = ["MarketDataAggregator::MarketDataAggregator.AggregatorFunction::FunctionHandler"]
  }

  logging_config {
    log_format            = "JSON"
    application_log_level = "INFO"
    system_log_level      = "INFO"
  }

  environment {
    variables = {
      MASSIVE_TOKEN                  = data.aws_secretsmanager_secret_version.massive_token.secret_string
      ASPNETCORE_ENVIRONMENT         = var.environment
      BATCH_SIZE                     = 250
      MARKET_DATA_BUCKET_NAME        = aws_s3_bucket.market_data.bucket
      MARKET_DATA_CATALOG_TABLE_NAME = aws_dynamodb_table.market_data.name
    }
  }

  kms_key_arn = data.aws_kms_key.lambda.arn
}

resource "aws_lambda_function" "market_data_orchestrator" {
  function_name = "${var.team}-${var.environment}-${local.market_data_orchestrator_service}"
  role          = aws_iam_role.market_data_aggregator_lambda.arn

  memory_size = 4096
  timeout     = 900

  architectures = ["x86_64"]

  package_type = "Image"
  image_uri    = data.aws_ecr_image.market_data_aggregator.image_uri

  image_config {
    command = ["MarketDataAggregator::MarketDataAggregator.OrchestratorFunction::FunctionHandler"]
  }

  logging_config {
    log_format            = "JSON"
    application_log_level = "INFO"
    system_log_level      = "INFO"
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

// ------- Market Data Aggregator Lambda Function -------

// ------- Backtest Lambda Function -------

resource "aws_lambda_function" "backtest_worker" {
  function_name = "${var.team}-${var.environment}-${local.backtest_worker_service}"
  role          = aws_iam_role.backtest_lambda.arn

  memory_size = 3008
  timeout     = 900

  architectures = ["x86_64"]

  package_type = "Image"
  image_uri    = data.aws_ecr_image.backtester.image_uri

  image_config {
    command = ["Backtest.Lambda::Backtest.Lambda.WorkerFunction::FunctionHandler"]
  }

  logging_config {
    log_format            = "JSON"
    application_log_level = "INFO"
    # INFO so platform.report records reach CloudWatch - they are the billing-exact
    # source for the Grafana live-cost dashboard (plans/09-canonical-logging.md).
    system_log_level = "INFO"
  }

  environment {
    variables = {
      MASSIVE_TOKEN = data.aws_secretsmanager_secret_version.massive_token.secret_string
    }
  }

  kms_key_arn = data.aws_kms_key.lambda.arn
}

resource "aws_lambda_function" "backtest_orchestrator" {
  function_name = "${var.team}-${var.environment}-${local.backtest_orchestrator_service}"
  role          = aws_iam_role.backtest_lambda.arn

  memory_size = 1024
  timeout     = 900

  architectures = ["x86_64"]

  package_type = "Image"
  image_uri    = data.aws_ecr_image.backtester.image_uri

  image_config {
    command = ["Backtest.Lambda::Backtest.Lambda.OrchestratorFunction::FunctionHandler"]
  }

  logging_config {
    log_format            = "JSON"
    application_log_level = "INFO"
    # INFO so platform.report records reach CloudWatch - they are the billing-exact
    # source for the Grafana live-cost dashboard (plans/09-canonical-logging.md).
    system_log_level = "INFO"
  }

  # environment {
  #   variables = local.backtest_common_environment
  # }

  kms_key_arn = data.aws_kms_key.lambda.arn
}

// ------- Backtest Lambda Function -------