locals {
  default_tags = {
    team        = var.team
    environment = var.environment
    repo        = "https://github.com/rwitzlib/stockmountain"
  }

  market_data_aggregator_service   = "market-data-aggregator"
  market_data_orchestrator_service = "market-data-orchestrator"

  backtest_worker_service       = "backtest-worker"
  backtest_orchestrator_service = "backtest-orchestrator"
  backtest_dispatcher_service   = "backtest-dispatcher"
  backtest_filter_service       = "backtest-filter"
  backtest_image_service        = "backtest-worker"

  web_service_name = "stockmountain-app"
}
