# resource "aws_dynamodb_table" "auth" {
#   name           = "${var.team}-${var.environment}-auth-store"
#   billing_mode   = "PROVISIONED"
#   read_capacity  = 1
#   write_capacity = 1
#   hash_key       = "pk"
#   range_key      = "sk"

#   attribute {
#     name = "pk"
#     type = "S"
#   }

#   attribute {
#     name = "sk"
#     type = "S"
#   }
# }

# resource "aws_dynamodb_table" "security_master" {
#   name         = "${var.team}-${var.environment}-security-master-store"
#   billing_mode = "PAY_PER_REQUEST"
#   hash_key     = "PK"

#   point_in_time_recovery {
#     enabled = true
#   }

#   deletion_protection_enabled = true

#   attribute {
#     name = "PK"
#     type = "S"
#   }
# }

# resource "aws_dynamodb_table" "meta" {
#   name         = "${var.team}-${var.environment}-meta-store"
#   billing_mode = "PAY_PER_REQUEST"
#   hash_key     = "PK"
#   range_key    = "SK"

#   point_in_time_recovery {
#     enabled = true
#   }

#   deletion_protection_enabled = true

#   attribute {
#     name = "PK"
#     type = "S"
#   }

#   attribute {
#     name = "SK"
#     type = "S"
#   }
# }

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

# resource "aws_dynamodb_table" "management_deploy" {
#   name           = "${var.team}-${var.environment}-management-deploy-store"
#   billing_mode   = "PROVISIONED"
#   read_capacity  = 1
#   write_capacity = 1
#   hash_key       = "Id"

#   attribute {
#     name = "Id"
#     type = "S"
#   }
# }
