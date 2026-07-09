resource "aws_s3_bucket" "market_data" {
  bucket = "${var.team}-${var.environment}-market-data-${var.region}"
}

resource "aws_s3_bucket_public_access_block" "market_data" {
  bucket = aws_s3_bucket.market_data.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "market_data" {
  bucket = aws_s3_bucket.market_data.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket" "backtest_data" {
  bucket = "${var.team}-${var.environment}-backtest-data"
}
