locals {
  api_service_name = "api"

  api_dynamodb_table_names = [
    aws_dynamodb_table.backtest.name,
    aws_dynamodb_table.market_data.name,
    aws_dynamodb_table.user.name,
    # "${var.team}-${var.environment}-marketviewer-strategy-store",
    # "${var.team}-${var.environment}-optimus-trade-store",
    # "${var.team}-${var.environment}-marketviewer-scan-store",
    # "${var.team}-${var.environment}-security-master-store",
    # "${var.team}-${var.environment}-meta-store"
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
      "s3:PutObject"
    ]

    resources = [
      "${aws_s3_bucket.market_data.arn}/*"
    ]
  }

  statement {
    sid    = "ListMarketDataBucket"
    effect = "Allow"

    actions = [
      "s3:ListBucket"
    ]

    resources = [
      aws_s3_bucket.market_data.arn
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
    AWS_ACCESS_KEY_ID     = aws_iam_access_key.api.id
    AWS_SECRET_ACCESS_KEY = aws_iam_access_key.api.secret
    AWS_REGION            = var.region
    AWS_DEFAULT_REGION    = var.region
  }

  sensitive = true
}
