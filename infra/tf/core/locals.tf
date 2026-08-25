locals {
  default_tags = {
    team        = var.team
    environment = var.environment
    repo        = "https://github.com/rwitzlib/stockmountain"
  }

  repositories = [
    "api",
    "backtester",
    "billing",
    "market-data-aggregator",
    "optimus"
    # "kesha",
    # "marketviewer-api",
    # "stockmountain-app",
  ]
}
