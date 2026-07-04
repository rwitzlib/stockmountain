resource "aws_dynamodb_table" "backtest" {
  name         = "${var.team}-${var.environment}-backtest-store"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "Id"

  point_in_time_recovery {
    enabled = true
  }

  deletion_protection_enabled = true

  attribute {
    name = "Id"
    type = "S"
  }

  attribute {
    name = "UserId"
    type = "S"
  }

  global_secondary_index {
    name            = "UserIndex"
    hash_key        = "UserId"
    projection_type = "ALL"
  }
}

resource "aws_dynamodb_table" "user" {
  name         = "${var.team}-${var.environment}-user-store"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "Id"

  point_in_time_recovery {
    enabled = true
  }

  deletion_protection_enabled = true

  attribute {
    name = "Id"
    type = "S"
  }
}

resource "aws_sqs_queue" "backtest_orchestrator" {
  name = "${var.team}-${var.environment}-backtest-orchestrator"
}

resource "aws_sqs_queue" "backtest_filter" {
  name = "${var.team}-${var.environment}-backtest-filter"
}

locals {
  backtest_common_environment = {
    ASPNETCORE_ENVIRONMENT                  = var.environment
    POLYGON_TOKEN                           = data.aws_ssm_parameter.polygon_token.value
    BacktestConfig__TableName               = aws_dynamodb_table.backtest.name
    BacktestConfig__RequestDetailsIndexName = "RequestDetailsIndex"
    BacktestConfig__UserIndexName           = "UserIndex"
    BacktestConfig__S3BucketName            = aws_s3_bucket.market_data.bucket
    UserConfig__TableName                   = aws_dynamodb_table.user.name
    WORKER_BATCH_SIZE                       = "100"
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

resource "aws_lambda_function" "backtest_dispatcher" {
  function_name = "${var.team}-${var.environment}-${local.backtest_dispatcher_service}"
  role          = aws_iam_role.backtest_lambda.arn

  memory_size = 512
  timeout     = 300

  architectures = ["x86_64"]

  package_type = "Image"
  image_uri    = data.aws_ecr_image.backtester.image_uri

  image_config {
    command = ["Backtest.Lambda::Backtest.Lambda.DispatcherFunction::FunctionHandler"]
  }

  environment {
    variables = merge(local.backtest_common_environment, {
      BacktestConfig__LambdaName = aws_lambda_function.backtester.function_name
      FILTER_QUEUE_URL           = aws_sqs_queue.backtest_filter.url
    })
  }

  kms_key_arn = data.aws_kms_key.lambda.arn
}

resource "aws_lambda_function" "backtest_filter" {
  function_name = "${var.team}-${var.environment}-${local.backtest_filter_service}"
  role          = aws_iam_role.backtest_lambda.arn

  memory_size = 2048
  timeout     = 900

  architectures = ["x86_64"]

  package_type = "Image"
  image_uri    = data.aws_ecr_image.backtester.image_uri

  image_config {
    command = ["Backtest.Lambda::Backtest.Lambda.FilterFunction::FunctionHandler"]
  }

  environment {
    variables = merge(local.backtest_common_environment, {
      BacktestConfig__LambdaName = aws_lambda_function.backtester.function_name
    })
  }

  kms_key_arn = data.aws_kms_key.lambda.arn
}

resource "aws_lambda_event_source_mapping" "backtest_dispatcher" {
  event_source_arn = aws_sqs_queue.backtest_orchestrator.arn
  function_name    = aws_lambda_function.backtest_dispatcher.arn
  batch_size       = 1
}

resource "aws_lambda_event_source_mapping" "backtest_filter" {
  event_source_arn = aws_sqs_queue.backtest_filter.arn
  function_name    = aws_lambda_function.backtest_filter.arn
  batch_size       = 1
}

resource "aws_lambda_permission" "backtest_dispatcher_sqs" {
  statement_id  = "AllowExecutionFromSQS"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.backtest_dispatcher.function_name
  principal     = "sqs.amazonaws.com"
  source_arn    = aws_sqs_queue.backtest_orchestrator.arn
}

resource "aws_lambda_permission" "backtest_filter_sqs" {
  statement_id  = "AllowExecutionFromSQS"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.backtest_filter.function_name
  principal     = "sqs.amazonaws.com"
  source_arn    = aws_sqs_queue.backtest_filter.arn
}
