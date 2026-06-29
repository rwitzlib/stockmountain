locals {
  repositories = [
    "backtest-worker",
    "backtest-orchestrator",
    "backtest-dispatcher",
    "aggregateception",
    "market-data-aggregator",
    "kesha",
    "marketviewer-api",
    "stockmountain-app",
  ]
}

resource "aws_ecr_repository" "this" {
  for_each = toset(local.repositories)

  name         = "${var.team}-${var.environment}-${each.value}"
  force_delete = true
}

resource "aws_ecr_lifecycle_policy" "expire_count" {
  for_each = toset(local.repositories)

  repository = aws_ecr_repository.this[each.key].name

  policy = <<EOF
{
    "rules": [
        {
            "rulePriority": 1,
            "description": "Keep last 5 images",
            "selection": {
                "tagStatus": "any",
                "countType": "imageCountMoreThan",
                "countNumber": 5
            },
            "action": {
                "type": "expire"
            }
        }
    ]
}
EOF

  depends_on = [aws_ecr_repository.this]
}
