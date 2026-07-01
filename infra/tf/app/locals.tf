locals {
  default_tags = {
    team        = var.team
    environment = var.environment
    repo        = "https://github.com/rwitzlib/stockmountain"
  }

  market_data_aggregator_service   = "market-data-aggregator"
  market_data_orchestrator_service = "market-data-orchestrator"

  web_service_name = "stockmountain-app"
}
