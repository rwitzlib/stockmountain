locals {
  backtest_common_environment = {
    ASPNETCORE_ENVIRONMENT        = var.environment
    POLYGON_TOKEN                 = data.aws_ssm_parameter.polygon_token.value
    BacktestConfig__TableName     = aws_dynamodb_table.backtest.name
    BacktestConfig__UserIndexName = "UserIndex"
    BacktestConfig__S3BucketName  = aws_s3_bucket.market_data.bucket
    UserConfig__TableName         = aws_dynamodb_table.user.name
    WORKER_BATCH_SIZE             = "100"
  }
}

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

  environment {
    variables = merge(local.backtest_common_environment, {
      BacktestConfig__LambdaName = "${var.team}-${var.environment}-${local.backtest_worker_service}"
    })
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

  environment {
    variables = merge(local.backtest_common_environment, {
      BacktestConfig__LambdaName = aws_lambda_function.backtester.function_name
    })
  }

  kms_key_arn = data.aws_kms_key.lambda.arn
}