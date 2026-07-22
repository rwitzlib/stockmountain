output "api_aws_environment_variables" {
  value = {
    AWS_ACCESS_KEY_ID            = aws_iam_access_key.api.id
    AWS_SECRET_ACCESS_KEY        = aws_iam_access_key.api.secret
    AWS_REGION                   = var.region
    AWS_DEFAULT_REGION           = var.region
    PORT                         = var.api_port
    ASPNETCORE_ENVIRONMENT       = var.environment
    MASSIVE_TOKEN                = var.massive_token
    CLERK_WEBHOOK_SIGNING_SECRET = var.clerk_webhook_signing_secret
    OTEL_EXPORTER_OTLP_HEADERS   = var.otel_exporter_otlp_headers
    OTEL_EXPORTER_OTLP_ENDPOINT  = var.otel_exporter_otlp_endpoint
    OTEL_EXPORTER_OTLP_PROTOCOL  = var.otel_exporter_otlp_protocol
    OTEL_RESOURCE_ATTRIBUTES     = var.otel_resource_attributes
    CACHE_WARMUP_ENABLED         = var.cache_warmup_enabled
    ALPACA_API_KEY_ID            = var.alpaca_api_key_id
    ALPACA_API_SECRET_KEY        = var.alpaca_api_secret_key

    # .NET config binding: Section__Key overrides appsettings. Resource names come
    # from terraform so appsettings.dev.json never carries literals (local stays literal).
    StrategyConfig__TableName   = aws_dynamodb_table.strategy.name
    TradeConfig__TableName      = aws_dynamodb_table.trade.name
    UserConfig__TableName       = aws_dynamodb_table.user.name
    BacktestConfig__TableName   = aws_dynamodb_table.backtest.name
    ScanConfig__TableName       = aws_dynamodb_table.scan.name
    MetaConfig__MetaTableName   = aws_dynamodb_table.meta.name
    MarketDataConfig__TableName = aws_dynamodb_table.market_data.name
    SignalQueue__QueueUrl       = aws_sqs_queue.strategy_signals.url
    SignalQueue__Enabled        = "true"
  }

  sensitive = true
}

output "optimus_aws_environment_variables" {
  value = {
    AWS_ACCESS_KEY_ID      = aws_iam_access_key.optimus.id
    AWS_SECRET_ACCESS_KEY  = aws_iam_access_key.optimus.secret
    AWS_REGION             = var.region
    AWS_DEFAULT_REGION     = var.region
    ASPNETCORE_ENVIRONMENT = var.environment
    MASSIVE_TOKEN          = var.massive_token
    ALPACA_API_KEY_ID      = var.alpaca_api_key_id
    ALPACA_API_SECRET_KEY  = var.alpaca_api_secret_key

    # .NET config binding: Section__Key overrides appsettings. Resource names come
    # from terraform so appsettings.dev.json never carries literals (local stays literal).
    StrategyConfig__TableName       = aws_dynamodb_table.strategy.name
    TradeConfig__TableName          = aws_dynamodb_table.trade.name
    UserConfig__TableName           = aws_dynamodb_table.user.name
    ScanResultsConfig__TableName    = aws_dynamodb_table.scan.name
    ExecutionDedupConfig__TableName = aws_dynamodb_table.execution_dedup.name
    MetaConfig__TableName           = aws_dynamodb_table.meta.name
    SqsConsumerConfig__QueueUrl     = aws_sqs_queue.strategy_signals.url
    SqsConsumerConfig__Enabled      = "true"
  }

  sensitive = true
}

output "web_aws_environment_variables" {
  value = {
    VITE_API_URL                 = var.api_url
    PORT                         = var.web_port
    CLERK_WEBHOOK_SIGNING_SECRET = var.clerk_webhook_signing_secret
  }

  sensitive = true
}