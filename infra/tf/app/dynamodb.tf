resource "aws_dynamodb_table" "market_data" {
  name         = "${var.team}-${var.environment}-market-data-store"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "PK"
  range_key    = "SK"

  point_in_time_recovery {
    enabled = true
  }

  deletion_protection_enabled = true

  attribute {
    name = "PK"
    type = "S"
  }

  attribute {
    name = "SK"
    type = "S"
  }
}

resource "aws_dynamodb_table" "user" {
  name         = "${var.team}-${var.environment}-user-store"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "Id"

  point_in_time_recovery {
    enabled = true
  }

  deletion_protection_enabled = true

  attribute {
    name = "Id"
    type = "S"
  }
}

# Single-table store for strategies: BOT#{id}/CONFIG items, BOT#{id}/STATE strategy
# state, BOT#{id}/BALANCE#{date} history, and the ACTIVE_STRATEGIES hash-refcount
# partition. TTL on Expiry implements soft-delete (StrategyRepository.Delete).
resource "aws_dynamodb_table" "strategy" {
  name         = "${var.team}-${var.environment}-marketviewer-strategy-store"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "PK"
  range_key    = "SK"

  point_in_time_recovery {
    enabled = true
  }

  deletion_protection_enabled = true

  ttl {
    attribute_name = "Expiry"
    enabled        = true
  }

  attribute {
    name = "PK"
    type = "S"
  }

  attribute {
    name = "SK"
    type = "S"
  }

  attribute {
    name = "UserId"
    type = "S"
  }

  attribute {
    name = "Visibility"
    type = "S"
  }

  attribute {
    name = "StrategyHash"
    type = "S"
  }

  global_secondary_index {
    name            = "UserIndex"
    hash_key        = "UserId"
    projection_type = "ALL"
  }

  global_secondary_index {
    name            = "VisibilityIndex"
    hash_key        = "Visibility"
    projection_type = "ALL"
  }

  global_secondary_index {
    name            = "StrategyHashIndex"
    hash_key        = "StrategyHash"
    projection_type = "ALL"
  }
}

resource "aws_dynamodb_table" "trade" {
  name         = "${var.team}-${var.environment}-optimus-trade-store"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "Id"

  point_in_time_recovery {
    enabled = true
  }

  deletion_protection_enabled = true

  attribute {
    name = "Id"
    type = "S"
  }

  attribute {
    name = "UserId"
    type = "S"
  }

  attribute {
    name = "StrategyId"
    type = "S"
  }

  global_secondary_index {
    name            = "UserIndex"
    hash_key        = "UserId"
    projection_type = "ALL"
  }

  global_secondary_index {
    name            = "StrategyIndex"
    hash_key        = "StrategyId"
    projection_type = "ALL"
  }
}

# Scan audit records: STRAT#{hash}/WINDOW#{window}, self-expiring after minutes.
# Ephemeral by design, so no PITR.
resource "aws_dynamodb_table" "scan" {
  name         = "${var.team}-${var.environment}-marketviewer-scan-store"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "PK"
  range_key    = "SK"

  deletion_protection_enabled = true

  ttl {
    attribute_name = "Expiry"
    enabled        = true
  }

  attribute {
    name = "PK"
    type = "S"
  }

  attribute {
    name = "SK"
    type = "S"
  }
}

# Idempotency records for trade execution: STRAT#{id}#TICKER#{ticker}/WINDOW#{window},
# conditional-write dedup, self-expiring. Ephemeral by design, so no PITR.
resource "aws_dynamodb_table" "execution_dedup" {
  name         = "${var.team}-${var.environment}-optimus-execution-dedup-store"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "PK"
  range_key    = "SK"

  deletion_protection_enabled = true

  ttl {
    attribute_name = "TTL"
    enabled        = true
  }

  attribute {
    name = "PK"
    type = "S"
  }

  attribute {
    name = "SK"
    type = "S"
  }
}

resource "aws_dynamodb_table" "meta" {
  name         = "${var.team}-${var.environment}-meta-store"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "PK"
  range_key    = "SK"

  point_in_time_recovery {
    enabled = true
  }

  deletion_protection_enabled = true

  attribute {
    name = "PK"
    type = "S"
  }

  attribute {
    name = "SK"
    type = "S"
  }
}

resource "aws_dynamodb_table" "backtest" {
  name         = "${var.team}-${var.environment}-backtest-store"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "PK"
  range_key    = "SK"

  point_in_time_recovery {
    enabled = true
  }

  deletion_protection_enabled = true

  attribute {
    name = "PK"
    type = "S"
  }

  attribute {
    name = "SK"
    type = "S"
  }

  attribute {
    name = "UserId"
    type = "S"
  }

  global_secondary_index {
    name            = "UserIndex"
    hash_key        = "UserId"
    projection_type = "ALL"
  }
}
