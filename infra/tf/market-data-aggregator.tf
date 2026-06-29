resource "aws_iam_role" "market_data_aggregator_lambda" {
  name = "${local.market_data_aggregator_service}-Lambda"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      },
    ]
  })

  managed_policy_arns = [
    "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
  ]

  inline_policy {
    name = "${local.market_data_aggregator_service}-Access"

    policy = jsonencode({
      Version = "2012-10-17"
      Statement = [
        {
          Effect = "Allow"
          Action = [
            "s3:PutObject",
            "s3:GetObject",
            "s3:ListBucket"
          ]
          Resource = [
            aws_s3_bucket.market_data.arn,
            "${aws_s3_bucket.market_data.arn}/tickerdetails/*",
            "${aws_s3_bucket.market_data.arn}/backtest/*"
          ]
        },
        {
          Effect = "Allow"
          Action = [
            "dynamodb:PutItem",
            "dynamodb:GetItem",
            "dynamodb:Query",
            "dynamodb:UpdateItem"
          ]
          Resource = aws_dynamodb_table.market_data.arn
        },
        {
          Effect = "Allow"
          Action = [
            "lambda:InvokeFunction"
          ]
          Resource = "arn:aws:lambda:${var.region}:${data.aws_caller_identity.current.account_id}:function:${var.team}-${var.environment}-${local.market_data_aggregator_service}"
        }
      ]
    })
  }
}

resource "aws_lambda_function" "market_data_aggregator" {
  function_name = "${var.team}-${var.environment}-${local.market_data_aggregator_service}"
  role          = aws_iam_role.market_data_aggregator_lambda.arn

  memory_size = 4096
  timeout     = 90

  architectures = ["x86_64"]

  package_type = "Image"
  image_uri    = data.aws_ecr_image.market_data_aggregator.image_uri

  image_config {
    command = ["MarketDataAggregator::MarketDataAggregator.Function::FunctionHandler"]
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

  kms_key_arn = "arn:aws:kms:us-east-2:100008144700:key/2a8095ff-9b60-4eba-9d59-06077c120a8b"
}

resource "aws_lambda_function" "market_data_orchestrator" {
  function_name = "${var.team}-${var.environment}-${local.market_data_orchestrator_service}"
  role          = aws_iam_role.market_data_aggregator_lambda.arn

  memory_size = 512
  timeout     = 900

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

  kms_key_arn = "arn:aws:kms:us-east-2:100008144700:key/2a8095ff-9b60-4eba-9d59-06077c120a8b"
}

resource "aws_cloudwatch_event_rule" "market_data_aggregator_daily" {
  name                = "daily-market-data-aggregate"
  description         = "gather aggregate market data from previous day and upload to S3"
  schedule_expression = "cron(0 11 * * ? *)"
}

resource "aws_cloudwatch_event_target" "market_data_aggregator_minute" {
  arn  = aws_lambda_function.market_data_aggregator.arn
  rule = aws_cloudwatch_event_rule.market_data_aggregator_daily.id

  input_transformer {
    input_template = <<EOF
{
  "type": "auto",
  "multiplier": 1,
  "timespan": "minute"
}
EOF
  }
}

resource "aws_cloudwatch_event_target" "market_data_aggregator_hour" {
  arn  = aws_lambda_function.market_data_aggregator.arn
  rule = aws_cloudwatch_event_rule.market_data_aggregator_daily.id

  input_transformer {
    input_template = <<EOF
{
  "type": "auto",
  "multiplier": 1,
  "timespan": "hour"
}
EOF
  }
}

resource "aws_cloudwatch_event_target" "market_data_aggregator_day" {
  arn  = aws_lambda_function.market_data_aggregator.arn
  rule = aws_cloudwatch_event_rule.market_data_aggregator_daily.id

  input_transformer {
    input_template = <<EOF
{
  "type": "auto",
  "multiplier": 1,
  "timespan": "day"
}
EOF
  }
}

resource "aws_lambda_permission" "market_data_aggregator_cloudwatch" {
  statement_id  = "AllowExecutionFromCloudWatch"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.market_data_aggregator.function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.market_data_aggregator_daily.arn
}
