locals {
  optimus_service_name = "optimus"
}

resource "aws_iam_user" "optimus" {
  name = "${var.team}-${var.environment}-optimus"
}

data "aws_iam_policy_document" "optimus_runtime" {
  statement {
    sid    = "ConsumeStrategySignals"
    effect = "Allow"

    actions = [
      "sqs:ReceiveMessage",
      "sqs:DeleteMessage",
      "sqs:GetQueueAttributes"
    ]

    resources = [
      aws_sqs_queue.strategy_signals.arn
    ]
  }

  statement {
    sid    = "ReadWriteOptimusTables"
    effect = "Allow"

    actions = [
      "dynamodb:DeleteItem",
      "dynamodb:GetItem",
      "dynamodb:PutItem",
      "dynamodb:Query",
      "dynamodb:Scan",
      "dynamodb:UpdateItem"
    ]

    resources = flatten([
      for table in [
        aws_dynamodb_table.strategy,
        aws_dynamodb_table.trade,
        aws_dynamodb_table.scan,
        aws_dynamodb_table.execution_dedup,
        aws_dynamodb_table.meta,
        aws_dynamodb_table.user
      ] : [table.arn, "${table.arn}/index/*"]
    ])
  }
}

resource "aws_iam_policy" "optimus" {
  name        = "${var.team}-${var.environment}-optimus-runtime"
  description = "Runtime permissions for optimus beyond the shared api policy."
  policy      = data.aws_iam_policy_document.optimus_runtime.json
}

resource "aws_iam_user_policy_attachment" "optimus_runtime" {
  user       = aws_iam_user.optimus.name
  policy_arn = aws_iam_policy.optimus.arn
}

resource "aws_iam_access_key" "optimus" {
  user = aws_iam_user.optimus.name
}

output "optimus_aws_environment_variables" {
  value = {
    AWS_ACCESS_KEY_ID      = aws_iam_access_key.optimus.id
    AWS_SECRET_ACCESS_KEY  = aws_iam_access_key.optimus.secret
    AWS_REGION             = var.region
    AWS_DEFAULT_REGION     = var.region
    ASPNETCORE_ENVIRONMENT = var.environment
    MASSIVE_TOKEN          = var.massive_token
    ALPACA_API_KEY_ID      = var.alpaca_api_key_id
    ALPACA_API_SECRET_KEY  = var.alpaca_api_secret_key

    # .NET config binding: Section__Key overrides appsettings. Resource names come
    # from terraform so appsettings.dev.json never carries literals (local stays literal).
    StrategyConfig__TableName       = aws_dynamodb_table.strategy.name
    TradeConfig__TableName          = aws_dynamodb_table.trade.name
    UserConfig__TableName           = aws_dynamodb_table.user.name
    ScanResultsConfig__TableName    = aws_dynamodb_table.scan.name
    ExecutionDedupConfig__TableName = aws_dynamodb_table.execution_dedup.name
    MetaConfig__TableName           = aws_dynamodb_table.meta.name
    SqsConsumerConfig__QueueUrl     = aws_sqs_queue.strategy_signals.url
    SqsConsumerConfig__Enabled      = "true"
  }

  sensitive = true
}
