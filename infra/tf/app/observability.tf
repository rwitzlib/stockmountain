data "http" "lambda_promtail_release" {
  count = var.enable_grafana_cloud_logs ? 1 : 0
  url   = "https://api.github.com/repos/grafana/lambda-promtail/releases/latest"

  request_headers = {
    Accept = "application/vnd.github+json"
  }

  lifecycle {
    postcondition {
      condition     = self.status_code == 200
      error_message = "Unable to resolve the latest lambda-promtail release from GitHub."
    }
  }
}

locals {
  lambda_promtail_version = var.enable_grafana_cloud_logs ? jsondecode(data.http.lambda_promtail_release[0].response_body).tag_name : ""
  lambda_promtail_s3_key  = "observability/lambda-promtail-${local.lambda_promtail_version}.zip"
}

resource "aws_s3_object_copy" "lambda_promtail" {
  count = var.enable_grafana_cloud_logs ? 1 : 0

  bucket = aws_s3_bucket.backtest_data.bucket
  key    = local.lambda_promtail_s3_key
  source = "grafanalabs-cf-templates/lambda-promtail/lambda-promtail-${local.lambda_promtail_version}.zip"
}

resource "aws_iam_role" "lambda_promtail" {
  count = var.enable_grafana_cloud_logs ? 1 : 0
  name  = "${var.team}-${var.environment}-grafana-lambda-promtail"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "lambda.amazonaws.com"
      }
    }]
  })
}

resource "aws_iam_role_policy" "lambda_promtail" {
  count = var.enable_grafana_cloud_logs ? 1 : 0
  name  = "cloudwatch-logs"
  role  = aws_iam_role.lambda_promtail[0].id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = [
        "logs:CreateLogGroup",
        "logs:CreateLogStream",
        "logs:PutLogEvents"
      ]
      Resource = "arn:aws:logs:${var.region}:${data.aws_caller_identity.current.account_id}:log-group:/aws/lambda/${var.team}-${var.environment}-grafana-lambda-promtail:*"
    }]
  })
}

resource "aws_cloudwatch_log_group" "lambda_promtail" {
  count             = var.enable_grafana_cloud_logs ? 1 : 0
  name              = "/aws/lambda/${var.team}-${var.environment}-grafana-lambda-promtail"
  retention_in_days = 14
}

resource "aws_lambda_function" "lambda_promtail" {
  count = var.enable_grafana_cloud_logs ? 1 : 0

  function_name = "${var.team}-${var.environment}-grafana-lambda-promtail"
  role          = aws_iam_role.lambda_promtail[0].arn
  handler       = "main"
  runtime       = "provided.al2023"
  timeout       = 60
  memory_size   = 128

  s3_bucket = aws_s3_bucket.backtest_data.bucket
  s3_key    = local.lambda_promtail_s3_key

  environment {
    variables = {
      WRITE_ADDRESS = var.grafana_loki_write_address
      USERNAME      = var.grafana_loki_username
      PASSWORD      = var.grafana_loki_token
      KEEP_STREAM   = "false"
      EXTRA_LABELS  = "environment,${var.environment}"
      RELABEL_CONFIGS = jsonencode([
        {
          source_labels = ["__aws_cloudwatch_log_group"]
          regex         = "/aws/lambda/(.+)"
          target_label  = "service_name"
          replacement   = "$1"
          action        = "replace"
        }
      ])
    }
  }

  lifecycle {
    precondition {
      condition = alltrue([
        var.grafana_loki_write_address != "",
        var.grafana_loki_username != "",
        var.grafana_loki_token != ""
      ])
      error_message = "Grafana Loki write address, username, and token are required when Grafana Cloud log forwarding is enabled."
    }
  }

  depends_on = [
    aws_s3_object_copy.lambda_promtail,
    aws_iam_role_policy.lambda_promtail,
    aws_cloudwatch_log_group.lambda_promtail
  ]
}

resource "aws_lambda_function_event_invoke_config" "lambda_promtail" {
  count                  = var.enable_grafana_cloud_logs ? 1 : 0
  function_name          = aws_lambda_function.lambda_promtail[0].function_name
  maximum_retry_attempts = 2
}

resource "aws_lambda_permission" "lambda_promtail_cloudwatch" {
  count = var.enable_grafana_cloud_logs ? 1 : 0

  statement_id   = "AllowCloudWatchLogs"
  action         = "lambda:InvokeFunction"
  function_name  = aws_lambda_function.lambda_promtail[0].function_name
  principal      = "logs.${var.region}.amazonaws.com"
  source_account = data.aws_caller_identity.current.account_id
  source_arn     = "arn:aws:logs:${var.region}:${data.aws_caller_identity.current.account_id}:log-group:/aws/lambda/${var.team}-${var.environment}-*:*"
}

resource "aws_cloudwatch_log_subscription_filter" "backtest_orchestrator_grafana" {
  count = var.enable_grafana_cloud_logs ? 1 : 0

  name            = "grafana-backtest-orchestrator"
  log_group_name  = "/aws/lambda/${aws_lambda_function.backtest_orchestrator.function_name}"
  destination_arn = aws_lambda_function.lambda_promtail[0].arn
  filter_pattern  = ""

  depends_on = [aws_lambda_permission.lambda_promtail_cloudwatch]
}

resource "aws_cloudwatch_log_subscription_filter" "backtest_worker_grafana" {
  count = var.enable_grafana_cloud_logs ? 1 : 0

  name            = "grafana-backtest-worker"
  log_group_name  = "/aws/lambda/${aws_lambda_function.backtest_worker.function_name}"
  destination_arn = aws_lambda_function.lambda_promtail[0].arn
  filter_pattern  = ""

  depends_on = [aws_lambda_permission.lambda_promtail_cloudwatch]
}

resource "aws_cloudwatch_log_subscription_filter" "market_data_aggregator_grafana" {
  count = var.enable_grafana_cloud_logs ? 1 : 0

  name            = "grafana-market-data-aggregator"
  log_group_name  = "/aws/lambda/${aws_lambda_function.market_data_aggregator.function_name}"
  destination_arn = aws_lambda_function.lambda_promtail[0].arn
  filter_pattern  = ""

  depends_on = [aws_lambda_permission.lambda_promtail_cloudwatch]
}

resource "aws_cloudwatch_log_subscription_filter" "market_data_orchestrator_grafana" {
  count = var.enable_grafana_cloud_logs ? 1 : 0

  name            = "grafana-market-data-orchestrator"
  log_group_name  = "/aws/lambda/${aws_lambda_function.market_data_orchestrator.function_name}"
  destination_arn = aws_lambda_function.lambda_promtail[0].arn
  filter_pattern  = ""

  depends_on = [aws_lambda_permission.lambda_promtail_cloudwatch]
}
