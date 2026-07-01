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