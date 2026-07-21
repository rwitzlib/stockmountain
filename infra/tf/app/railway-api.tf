locals {
  api_service_name = "api"

  api_dynamodb_table_names = [
    aws_dynamodb_table.backtest.name,
    aws_dynamodb_table.market_data.name,
    aws_dynamodb_table.user.name,
    aws_dynamodb_table.strategy.name,
    aws_dynamodb_table.trade.name,
    aws_dynamodb_table.scan.name,
    aws_dynamodb_table.meta.name,
    # "${var.team}-${var.environment}-security-master-store",
  ]

  api_dynamodb_table_arns = [
    for table_name in local.api_dynamodb_table_names :
    "arn:aws:dynamodb:${var.region}:${data.aws_caller_identity.current.account_id}:table/${table_name}"
  ]
}

resource "aws_iam_user" "api" {
  name = "${var.team}-${var.environment}-api"
}

data "aws_iam_policy_document" "api_runtime" {
  statement {
    sid    = "ReadWriteMarketDataObjects"
    effect = "Allow"

    actions = [
      "s3:GetObject",
      "s3:PutObject",
      "s3:ListBucket"
    ]

    resources = [
      aws_s3_bucket.market_data.arn,
      "${aws_s3_bucket.market_data.arn}/*",
      aws_s3_bucket.backtest_data.arn,
      "${aws_s3_bucket.backtest_data.arn}/*"
    ]
  }

  statement {
    sid    = "ReadWriteRuntimeTables"
    effect = "Allow"

    actions = [
      "dynamodb:DeleteItem",
      "dynamodb:GetItem",
      "dynamodb:PutItem",
      "dynamodb:Query",
      "dynamodb:Scan",
      "dynamodb:UpdateItem"
    ]

    resources = concat(
      local.api_dynamodb_table_arns,
      [
        for table_arn in local.api_dynamodb_table_arns :
        "${table_arn}/index/*"
      ]
    )
  }

  statement {
    sid    = "InvokeRuntimeFunctions"
    effect = "Allow"

    actions = [
      "lambda:InvokeFunction"
    ]

    resources = [
      aws_lambda_function.backtest_orchestrator.arn,
      aws_lambda_function.market_data_orchestrator.arn
    ]
  }

  statement {
    sid    = "PublishStrategySignals"
    effect = "Allow"

    actions = [
      "sqs:SendMessage"
    ]

    resources = [
      aws_sqs_queue.strategy_signals.arn
    ]
  }
}

resource "aws_iam_policy" "api" {
  name        = "${var.team}-${var.environment}-api-runtime"
  description = "Runtime permissions for the API."
  policy      = data.aws_iam_policy_document.api_runtime.json
}

resource "aws_iam_user_policy_attachment" "api_runtime" {
  user       = aws_iam_user.api.name
  policy_arn = aws_iam_policy.api.arn
}

resource "aws_iam_access_key" "api" {
  user = aws_iam_user.api.name
}

output "api_aws_access_key_id" {
  value = aws_iam_access_key.api.id
}

output "api_aws_secret_access_key" {
  value     = aws_iam_access_key.api.secret
  sensitive = true
}

output "api_aws_environment_variables" {
  value = {
    AWS_ACCESS_KEY_ID            = aws_iam_access_key.api.id
    AWS_SECRET_ACCESS_KEY        = aws_iam_access_key.api.secret
    AWS_REGION                   = var.region
    AWS_DEFAULT_REGION           = var.region
    PORT                         = var.api_port
    ASPNETCORE_ENVIRONMENT       = var.environment
    MASSIVE_TOKEN                = var.massive_token
    CLERK_WEBHOOK_SIGNING_SECRET = var.clerk_webhook_signing_secret
    OTEL_EXPORTER_OTLP_HEADERS   = var.otel_exporter_otlp_headers
    OTEL_EXPORTER_OTLP_ENDPOINT  = var.otel_exporter_otlp_endpoint
    OTEL_EXPORTER_OTLP_PROTOCOL  = var.otel_exporter_otlp_protocol
    OTEL_RESOURCE_ATTRIBUTES     = var.otel_resource_attributes

    # .NET config binding: Section__Key overrides appsettings. Resource names come
    # from terraform so appsettings.dev.json never carries literals (local stays literal).
    StrategyConfig__TableName   = aws_dynamodb_table.strategy.name
    TradeConfig__TableName      = aws_dynamodb_table.trade.name
    UserConfig__TableName       = aws_dynamodb_table.user.name
    BacktestConfig__TableName   = aws_dynamodb_table.backtest.name
    ScanConfig__TableName       = aws_dynamodb_table.scan.name
    MetaConfig__MetaTableName   = aws_dynamodb_table.meta.name
    MarketDataConfig__TableName = aws_dynamodb_table.market_data.name
    SignalQueue__QueueUrl       = aws_sqs_queue.strategy_signals.url
    SignalQueue__Enabled        = "true"
  }

  sensitive = true
}
