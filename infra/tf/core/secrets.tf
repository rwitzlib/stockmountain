resource "aws_secretsmanager_secret" "massive_token" {
  name        = "${var.team}-${var.environment}-massive-token"
  description = "Massive token for ${var.team} in ${var.environment}"
  tags = {
    Team        = var.team
    Environment = var.environment
  }
}

resource "aws_secretsmanager_secret_version" "massive_token" {
  secret_id     = aws_secretsmanager_secret.massive_token.id
  secret_string = var.massive_token
}