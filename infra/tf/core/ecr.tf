resource "aws_ecr_repository" "this" {
  for_each = toset(local.repositories)

  name         = "${var.team}-${var.environment}-${each.value}"
  force_delete = true
}

resource "aws_ecr_lifecycle_policy" "expire_count" {
  for_each = toset(local.repositories)

  repository = aws_ecr_repository.this[each.key].name

  # Each build pushes 2-3 artifacts (tagged image + untagged manifest/attestation),
  # and a Lambda keeps referencing its deployed digest until the next deploy. At
  # "keep 5" a couple of pushes without a redeploy expired the image a live Lambda
  # pointed at (2026-08-18, market-data-aggregator): async invokes were dropped
  # silently and the 08-19 minute file was never written. 30 ≈ 10+ builds of headroom.
  policy = <<EOF
{
    "rules": [
        {
            "rulePriority": 1,
            "description": "Keep last 30 images",
            "selection": {
                "tagStatus": "any",
                "countType": "imageCountMoreThan",
                "countNumber": 30
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

output "ecr_repository_urls" {
  value = { for name, repo in aws_ecr_repository.this : name => repo.repository_url }
}
