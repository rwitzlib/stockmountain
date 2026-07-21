resource "aws_sqs_queue" "backtest_orchestrator" {
  name = "${var.team}-${var.environment}-backtest-orchestrator"
}

resource "aws_sqs_queue" "backtest_filter" {
  name = "${var.team}-${var.environment}-backtest-filter"
}

resource "aws_sqs_queue" "strategy_signals_dlq" {
  name                      = "${var.team}-${var.environment}-strategy-signals-dlq"
  message_retention_seconds = 1209600
}

resource "aws_sqs_queue" "strategy_signals" {
  name = "${var.team}-${var.environment}-strategy-signals"

  # Entry signals go stale fast; drop anything a downed consumer misses rather
  # than buying on old prices when it comes back.
  message_retention_seconds  = 300
  visibility_timeout_seconds = 60

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.strategy_signals_dlq.arn
    maxReceiveCount     = 3
  })
}

output "strategy_signals_queue_url" {
  value = aws_sqs_queue.strategy_signals.url
}
