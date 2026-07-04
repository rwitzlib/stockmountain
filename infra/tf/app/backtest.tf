locals {
  backtest_common_environment = {

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
    variables = local.backtest_common_environment
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
    variables = local.backtest_common_environment
  }

  kms_key_arn = data.aws_kms_key.lambda.arn
}