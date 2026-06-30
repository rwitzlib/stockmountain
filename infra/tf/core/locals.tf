locals {
  default_tags = {
    team        = var.team
    environment = var.environment
    repo        = "https://github.com/rwitzlib/stockmountain"
  }

  repositories = [
    # "backtest-worker",
    # "backtest-orchestrator",
    # "backtest-dispatcher",
    # "aggregateception",
    "market-data-aggregator",
    "market-data-orchestrator",
    # "kesha",
    # "marketviewer-api",
    # "stockmountain-app",
  ]
}
