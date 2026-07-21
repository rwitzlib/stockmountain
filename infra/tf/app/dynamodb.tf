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
