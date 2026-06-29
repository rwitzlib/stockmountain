locals {
  default_tags = {
    Team        = var.team
    Environment = var.environment
    Repo        = "https://github.com/rwitzlib/stockmountain"
  }

  market_data_aggregator_service   = "market-data-aggregator"
  market_data_orchestrator_service = "market-data-orchestrator"

  kesha_team            = "lad"
  kesha_business_domain = "marketviewer"
  kesha_service         = "kesha"

  web_service_name = "stockmountain-app"
}
