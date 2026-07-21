resource "aws_cloudwatch_event_rule" "market_data_aggregator_daily" {
  name                = "daily-market-data-aggregate"
  description         = "gather aggregate market data from previous day and upload to S3"
  # 06:00 UTC = 1-2am ET: session data is final, and files are ready before the
  # API's daily cache re-warm at 3:30am ET (which runs before 4am pre-market).
  schedule_expression = "cron(0 6 * * ? *)"
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