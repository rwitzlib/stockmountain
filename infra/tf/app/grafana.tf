# Grafana Cloud dashboards + alerting for live Lambda cost and backtest operations
# (plans/09-canonical-logging.md). Cost math is derived from platform.report records
# (billing-exact) shipped to Loki by lambda-promtail; wide events ("canonical" lines)
# provide per-backtest / per-user attribution. Skipped entirely unless both
# grafana_url and grafana_api_key are set.

locals {
  grafana_enabled = var.grafana_url != "" && var.grafana_api_key != ""

  loki_ds = {
    type = "loki"
    uid  = var.grafana_loki_datasource_uid
  }
  expr_ds = {
    type = "__expr__"
    uid  = "__expr__"
  }

  lambda_fns = [
    {
      label = "backtest-worker"
      name  = aws_lambda_function.backtest_worker.function_name
      mem   = aws_lambda_function.backtest_worker.memory_size
    },
    {
      label = "backtest-orchestrator"
      name  = aws_lambda_function.backtest_orchestrator.function_name
      mem   = aws_lambda_function.backtest_orchestrator.memory_size
    },
    {
      label = "market-data-aggregator"
      name  = aws_lambda_function.market_data_aggregator.function_name
      mem   = aws_lambda_function.market_data_aggregator.memory_size
    },
    {
      label = "market-data-orchestrator"
      name  = aws_lambda_function.market_data_orchestrator.function_name
      mem   = aws_lambda_function.market_data_orchestrator.memory_size
    },
  ]

  all_lambda_regex = "${var.team}-${var.environment}-.*"

  # Lambda JSON log format wraps platform metrics as {"type":"platform.report","record":{"metrics":{...}}}
  # and application stdout as {"level":...,"message":"<wide event json>"}.
  report_pipe = " |= `platform.report` | json | type=`platform.report` | __error__=`` "
  wide_pipe   = " |= `canonical` | json | line_format `{{.message}}` | json | event=`canonical` | __error__=`` "

  price_gbs_str = format("%.10f", var.lambda_price_per_gb_second)
  price_req_str = format("%.7f", var.lambda_price_per_request)

  # GB-seconds cost per function: billedDurationMs / 1000 * memoryMB / 1024 * $/GB-s.
  # Memory is multiplied in from the terraform resource since LogQL cannot multiply
  # two unwrapped fields.
  fn_cost_exprs = { for f in local.lambda_fns : f.label => {
    interval = format(
      "sum(sum_over_time({service_name=`%s`}%s| unwrap record_metrics_billedDurationMs [$__interval])) / 1000 / 1024 * %d * $price_gbs",
      f.name, local.report_pipe, f.mem
    )
    range_24h = format(
      "(sum(sum_over_time({service_name=`%s`}%s| unwrap record_metrics_billedDurationMs [24h])) or vector(0)) / 1000 / 1024 * %d",
      f.name, local.report_pipe, f.mem
    )
  } }

  spend_range_expr = format("%s + (sum(count_over_time({service_name=~`%s`} |= `platform.report` [$__range])) or vector(0)) * $price_req", join(" + ", [
    for f in local.lambda_fns : format(
      "(sum(sum_over_time({service_name=`%s`}%s| unwrap record_metrics_billedDurationMs [$__range])) or vector(0)) / 1000 / 1024 * %d * $price_gbs",
      f.name, local.report_pipe, f.mem
    )
  ]), local.all_lambda_regex)

  # Alerting cannot use dashboard variables, so prices are baked in from terraform vars.
  spend_24h_expr = format("(%s) * %s + (sum(count_over_time({service_name=~`%s`} |= `platform.report` [24h])) or vector(0)) * %s",
    join(" + ", [for f in local.lambda_fns : local.fn_cost_exprs[f.label].range_24h]),
    local.price_gbs_str,
    local.all_lambda_regex,
    local.price_req_str
  )

  gbs_expr_window = { for w in ["1h", "24h"] : w => join(" + ", [
    for f in local.lambda_fns : format(
      "(sum(sum_over_time({service_name=`%s`}%s| unwrap record_metrics_billedDurationMs [%s])) or vector(0)) / 1000 / 1024 * %d",
      f.name, local.report_pipe, w, f.mem
    )
  ]) }

  worker_selector = "{service_name=`${aws_lambda_function.backtest_worker.function_name}`}"
  orch_selector   = "{service_name=`${aws_lambda_function.backtest_orchestrator.function_name}`}"
  backtest_regex  = "{service_name=~`${var.team}-${var.environment}-backtest-.*`}"

  pricing_variables = [
    {
      name    = "price_gbs"
      label   = "Price per GB-second (USD)"
      type    = "textbox"
      query   = local.price_gbs_str
      current = { text = local.price_gbs_str, value = local.price_gbs_str }
      options = []
      hide    = 0
    },
    {
      name    = "price_req"
      label   = "Price per request (USD)"
      type    = "textbox"
      query   = local.price_req_str
      current = { text = local.price_req_str, value = local.price_req_str }
      options = []
      hide    = 0
    },
  ]

  lambda_cost_dashboard = {
    uid           = "sm-lambda-cost"
    title         = "Lambda Live Cost"
    schemaVersion = 39
    editable      = true
    time          = { from = "now-24h", to = "now" }
    templating    = { list = local.pricing_variables }
    panels = [
      {
        id          = 1
        type        = "stat"
        title       = "Est. Lambda spend (selected range)"
        gridPos     = { h = 6, w = 8, x = 0, y = 0 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "currencyUSD", decimals = 4 }, overrides = [] }
        options     = { reduceOptions = { calcs = ["lastNotNull"] } }
        targets = [{
          refId      = "A"
          datasource = local.loki_ds
          expr       = local.spend_range_expr
          instant    = true
          queryType  = "instant"
        }]
      },
      {
        id          = 2
        type        = "stat"
        title       = "Invocations (selected range)"
        gridPos     = { h = 6, w = 8, x = 8, y = 0 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "short", decimals = 0 }, overrides = [] }
        options     = { reduceOptions = { calcs = ["lastNotNull"] } }
        targets = [{
          refId      = "A"
          datasource = local.loki_ds
          expr       = format("sum(count_over_time({service_name=~`%s`} |= `platform.report` [$__range]))", local.all_lambda_regex)
          instant    = true
          queryType  = "instant"
        }]
      },
      {
        id          = 3
        type        = "stat"
        title       = "GB-seconds (selected range)"
        gridPos     = { h = 6, w = 8, x = 16, y = 0 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "short", decimals = 0 }, overrides = [] }
        options     = { reduceOptions = { calcs = ["lastNotNull"] } }
        targets = [{
          refId      = "A"
          datasource = local.loki_ds
          expr = join(" + ", [
            for f in local.lambda_fns : format(
              "(sum(sum_over_time({service_name=`%s`}%s| unwrap record_metrics_billedDurationMs [$__range])) or vector(0)) / 1000 / 1024 * %d",
              f.name, local.report_pipe, f.mem
            )
          ])
          instant   = true
          queryType = "instant"
        }]
      },
      {
        id          = 4
        type        = "timeseries"
        title       = "Est. cost by function (per interval)"
        gridPos     = { h = 9, w = 12, x = 0, y = 6 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "currencyUSD" }, overrides = [] }
        targets = [for i, f in local.lambda_fns : {
          refId        = substr("ABCDEFGH", i, 1)
          datasource   = local.loki_ds
          expr         = local.fn_cost_exprs[f.label].interval
          legendFormat = f.label
        }]
      },
      {
        id          = 5
        type        = "timeseries"
        title       = "Invocations by function"
        gridPos     = { h = 9, w = 12, x = 12, y = 6 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "short" }, overrides = [] }
        targets = [for i, f in local.lambda_fns : {
          refId        = substr("ABCDEFGH", i, 1)
          datasource   = local.loki_ds
          expr         = format("sum(count_over_time({service_name=`%s`} |= `platform.report` [$__interval]))", f.name)
          legendFormat = f.label
        }]
      },
      {
        id          = 6
        type        = "timeseries"
        title       = "Max memory used by function (MB)"
        gridPos     = { h = 9, w = 12, x = 0, y = 15 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "short" }, overrides = [] }
        targets = [for i, f in local.lambda_fns : {
          refId        = substr("ABCDEFGH", i, 1)
          datasource   = local.loki_ds
          expr         = format("max(max_over_time({service_name=`%s`}%s| unwrap record_metrics_maxMemoryUsedMB [$__interval]))", f.name, local.report_pipe)
          legendFormat = "${f.label} (limit ${f.mem})"
        }]
      },
      {
        id          = 7
        type        = "timeseries"
        title       = "Backtest crashes (REPORT without wide event)"
        gridPos     = { h = 9, w = 12, x = 12, y = 15 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "short" }, overrides = [] }
        targets = [
          {
            refId        = "A"
            datasource   = local.loki_ds
            expr         = format("(sum(count_over_time(%s |= `platform.report` [$__interval])) or vector(0)) - (sum(count_over_time(%s%s[$__interval])) or vector(0))", local.worker_selector, local.worker_selector, local.wide_pipe)
            legendFormat = "backtest-worker"
          },
          {
            refId        = "B"
            datasource   = local.loki_ds
            expr         = format("(sum(count_over_time(%s |= `platform.report` [$__interval])) or vector(0)) - (sum(count_over_time(%s%s[$__interval])) or vector(0))", local.orch_selector, local.orch_selector, local.wide_pipe)
            legendFormat = "backtest-orchestrator"
          },
        ]
      },
      {
        id          = 8
        type        = "timeseries"
        title       = "Cold start init duration (ms)"
        gridPos     = { h = 8, w = 24, x = 0, y = 24 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "ms" }, overrides = [] }
        targets = [for i, f in local.lambda_fns : {
          refId        = substr("ABCDEFGH", i, 1)
          datasource   = local.loki_ds
          expr         = format("max(max_over_time({service_name=`%s`}%s| unwrap record_metrics_initDurationMs [$__interval]))", f.name, local.report_pipe)
          legendFormat = f.label
        }]
      },
    ]
  }

  backtest_ops_dashboard = {
    uid           = "sm-backtest-ops"
    title         = "Backtest Operations"
    schemaVersion = 39
    editable      = true
    time          = { from = "now-24h", to = "now" }
    templating    = { list = [] }
    panels = [
      {
        id          = 1
        type        = "timeseries"
        title       = "Backtests by final status"
        gridPos     = { h = 8, w = 12, x = 0, y = 0 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "short" }, overrides = [] }
        targets = [{
          refId        = "A"
          datasource   = local.loki_ds
          expr         = format("sum by (backtest_status) (count_over_time(%s%s[$__interval]))", local.orch_selector, local.wide_pipe)
          legendFormat = "{{backtest_status}}"
        }]
      },
      {
        id          = 2
        type        = "timeseries"
        title       = "Est. cost by user (USD)"
        gridPos     = { h = 8, w = 12, x = 12, y = 0 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "currencyUSD" }, overrides = [] }
        targets = [{
          refId        = "A"
          datasource   = local.loki_ds
          expr         = format("sum by (user_id) (sum_over_time(%s%s| unwrap est_cost_usd [$__interval]))", local.backtest_regex, local.wide_pipe)
          legendFormat = "{{user_id}}"
        }]
      },
      {
        id          = 3
        type        = "table"
        title       = "Top backtests by est. cost (selected range)"
        gridPos     = { h = 9, w = 12, x = 0, y = 8 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "currencyUSD", decimals = 4 }, overrides = [] }
        targets = [{
          refId      = "A"
          datasource = local.loki_ds
          expr       = format("topk(10, sum by (backtest_id, user_id) (sum_over_time(%s%s| unwrap est_cost_usd [$__range])))", local.backtest_regex, local.wide_pipe)
          instant    = true
          queryType  = "instant"
          format     = "table"
        }]
      },
      {
        id          = 4
        type        = "timeseries"
        title       = "Worker duration (ms)"
        gridPos     = { h = 9, w = 12, x = 12, y = 8 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "ms" }, overrides = [] }
        targets = [
          {
            refId        = "A"
            datasource   = local.loki_ds
            expr         = format("quantile_over_time(0.5, %s%s| unwrap duration_ms [$__interval])", local.worker_selector, local.wide_pipe)
            legendFormat = "p50"
          },
          {
            refId        = "B"
            datasource   = local.loki_ds
            expr         = format("quantile_over_time(0.95, %s%s| unwrap duration_ms [$__interval])", local.worker_selector, local.wide_pipe)
            legendFormat = "p95"
          },
        ]
      },
      {
        id          = 5
        type        = "timeseries"
        title       = "Dropped signals & worker errors"
        gridPos     = { h = 8, w = 12, x = 0, y = 17 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "short" }, overrides = [] }
        targets = [
          {
            refId        = "A"
            datasource   = local.loki_ds
            expr         = format("sum(sum_over_time(%s%s| unwrap dropped_signal_count [$__interval]))", local.worker_selector, local.wide_pipe)
            legendFormat = "dropped signals"
          },
          {
            refId        = "B"
            datasource   = local.loki_ds
            expr         = format("sum(count_over_time(%s%s| outcome=`error` [$__interval]))", local.worker_selector, local.wide_pipe)
            legendFormat = "worker errors"
          },
        ]
      },
      {
        id          = 6
        type        = "timeseries"
        title       = "Filter cache hit ratio"
        gridPos     = { h = 8, w = 12, x = 12, y = 17 }
        datasource  = local.loki_ds
        fieldConfig = { defaults = { unit = "percentunit", max = 1 }, overrides = [] }
        targets = [{
          refId      = "A"
          datasource = local.loki_ds
          expr = format(
            "sum(sum_over_time(%s%s| unwrap filter_cache_hits [$__interval])) / (sum(sum_over_time(%s%s| unwrap filter_cache_hits [$__interval])) + sum(sum_over_time(%s%s| unwrap filter_cache_misses [$__interval])))",
            local.worker_selector, local.wide_pipe, local.worker_selector, local.wide_pipe, local.worker_selector, local.wide_pipe
          )
          legendFormat = "hit ratio"
        }]
      },
    ]
  }
}

resource "grafana_folder" "stockmountain" {
  count = local.grafana_enabled ? 1 : 0

  title = "StockMountain (${var.environment})"
  uid   = "sm-${var.environment}"
}

resource "grafana_dashboard" "lambda_cost" {
  count = local.grafana_enabled ? 1 : 0

  folder      = grafana_folder.stockmountain[0].uid
  config_json = jsonencode(local.lambda_cost_dashboard)
}

resource "grafana_dashboard" "backtest_ops" {
  count = local.grafana_enabled ? 1 : 0

  folder      = grafana_folder.stockmountain[0].uid
  config_json = jsonencode(local.backtest_ops_dashboard)
}

resource "grafana_contact_point" "cost_alerts_email" {
  count = local.grafana_enabled ? 1 : 0

  name = "stockmountain-cost-alerts"

  email {
    addresses = [var.grafana_alert_email]
  }
}

resource "grafana_rule_group" "lambda_cost" {
  count = local.grafana_enabled ? 1 : 0

  name             = "lambda-cost"
  folder_uid       = grafana_folder.stockmountain[0].uid
  interval_seconds = 300

  rule {
    name      = "Daily Lambda spend above threshold"
    condition = "B"

    no_data_state  = "OK"
    exec_err_state = "Error"
    for            = "0s"

    annotations = {
      summary = format("Estimated Lambda spend over the trailing 24h exceeded $%v.", var.lambda_daily_cost_alert_usd)
    }

    data {
      ref_id         = "A"
      datasource_uid = var.grafana_loki_datasource_uid
      relative_time_range {
        from = 86400
        to   = 0
      }
      model = jsonencode({
        refId     = "A"
        queryType = "instant"
        expr      = local.spend_24h_expr
      })
    }

    data {
      ref_id         = "B"
      datasource_uid = "__expr__"
      relative_time_range {
        from = 0
        to   = 0
      }
      model = jsonencode({
        refId      = "B"
        type       = "threshold"
        datasource = local.expr_ds
        expression = "A"
        conditions = [{
          evaluator = {
            type   = "gt"
            params = [var.lambda_daily_cost_alert_usd]
          }
        }]
      })
    }

    notification_settings {
      contact_point = grafana_contact_point.cost_alerts_email[0].name
    }
  }

  rule {
    name      = "Lambda GB-seconds rate spike"
    condition = "C"

    no_data_state  = "OK"
    exec_err_state = "Error"
    for            = "15m"

    annotations = {
      summary = "Lambda GB-seconds burned in the last hour is more than 5x the trailing-24h hourly average."
    }

    data {
      ref_id         = "A"
      datasource_uid = var.grafana_loki_datasource_uid
      relative_time_range {
        from = 3600
        to   = 0
      }
      model = jsonencode({
        refId     = "A"
        queryType = "instant"
        expr      = local.gbs_expr_window["1h"]
      })
    }

    data {
      ref_id         = "B"
      datasource_uid = var.grafana_loki_datasource_uid
      relative_time_range {
        from = 86400
        to   = 0
      }
      model = jsonencode({
        refId     = "B"
        queryType = "instant"
        expr      = "(${local.gbs_expr_window["24h"]}) / 24"
      })
    }

    data {
      ref_id         = "C"
      datasource_uid = "__expr__"
      relative_time_range {
        from = 0
        to   = 0
      }
      model = jsonencode({
        refId      = "C"
        type       = "math"
        datasource = local.expr_ds
        # +100 GB-s floor so a quiet account jumping from ~zero does not page.
        expression = "$A > 5 * $B + 100"
      })
    }

    notification_settings {
      contact_point = grafana_contact_point.cost_alerts_email[0].name
    }
  }
}
