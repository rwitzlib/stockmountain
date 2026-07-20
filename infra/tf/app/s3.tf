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

# Public share snapshots (shares/{shareId}.json) expire automatically; the payload's
# expiresAt is display-only. Expiry runs on S3's daily sweep, so links can outlive
# the 30-day mark by up to ~48h.
resource "aws_s3_bucket_lifecycle_configuration" "backtest_data" {
  bucket = aws_s3_bucket.backtest_data.id

  rule {
    id     = "expire-shares"
    status = "Enabled"

    filter {
      prefix = "shares/"
    }

    expiration {
      days = 30
    }
  }
}
