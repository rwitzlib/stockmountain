resource "aws_sqs_queue" "backtest_orchestrator" {
  name = "${var.team}-${var.environment}-backtest-orchestrator"
}

resource "aws_sqs_queue" "backtest_filter" {
  name = "${var.team}-${var.environment}-backtest-filter"
}
