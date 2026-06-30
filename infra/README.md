# StockMountain AWS Infrastructure

Terraform split into **core** (foundational platform) and **app** (services) stacks. One state file per stack, environment, and region.

## Layout

```
infra/tf/
  bootstrap/          # S3 state bucket + DynamoDB lock table (local state)
  config/             # Backend config files per environment and stack
  core/               # ECR repos, shared IAM, future VPC/networking
  app/                # Lambdas, DynamoDB, S3, deploy triggers
```

Former per-service roots under `infra/aws`, `infra/management`, `infra/aggregateception`, and `infra/kesha` are deprecated. `apps/web/tf` deploy resources live in `app/web-deploy.tf`.

## State backends

Both stacks share the same S3 bucket and lock table (created by bootstrap), with separate state keys:

| Stack | Dev state key |
|-------|---------------|
| Core | `stockmountain-dev-us-east-2.core.tfstate` |
| App | `stockmountain-dev-us-east-2.app.tfstate` |

| Setting | Dev value |
|---------|-----------|
| Bucket | `stockmountain-dev-terraform-state-us-east-2` |
| Lock table | `stockmountain-dev-terraform-locks-us-east-2` |
| Region | `us-east-2` |

## Bootstrap (first time only)

Creates the remote state bucket and lock table using local state:

```powershell
cd infra/tf/bootstrap
terraform init
terraform apply
```

## Core stack

Apply core before pushing container images — ECR repositories must exist first. CI runs this automatically before the main deploy job.

```powershell
cd infra/tf/core
terraform init -backend-config=../config/dev.core.backend.hcl
terraform plan -var-file=../../dev.tfvars
terraform apply -var-file=../../dev.tfvars
```

## App stack

Apply after images are pushed to ECR (Lambdas reference images by tag).

```powershell
cd infra/tf/app
terraform init -backend-config=../config/dev.app.backend.hcl
terraform plan -var-file=../../dev.tfvars
terraform apply -var-file=../../dev.tfvars
```

## Web app deploy (CI)

The management API deploy trigger is gated behind `enable_web_deploy`. CI should target only that resource:

```powershell
cd infra/tf/app
terraform apply `
  -backend-config=../config/dev.app.backend.hcl `
  -var-file=../../dev.tfvars `
  -var="image_tag=$IMAGE_TAG" `
  -var="deploy_run_id=$GITHUB_RUN_ID" `
  -var="deploy_actor=$GITHUB_ACTOR" `
  -var="enable_web_deploy=true" `
  -target=restapi_object.web_app_deploy
```

## Migrating from the monolithic stack

If you previously applied the flat `infra/tf` root, move resources into the new stacks before deleting the old state:

```powershell
# 1. Pull existing state
cd infra/tf
terraform init -backend-config=config/dev.backend.hcl   # if you still have the old config
terraform state pull > old.tfstate

# 2. Initialize new stacks
cd ../core
terraform init -backend-config=../config/dev.core.backend.hcl

cd ../app
terraform init -backend-config=../config/dev.app.backend.hcl

# 3. Move ECR/IAM resources to core, everything else to app
# Example (adjust addresses to match your old state):
# terraform state mv -state=old.tfstate -state-out=../core/terraform.tfstate aws_ecr_repository.this[\"market-data-aggregator\"] aws_ecr_repository.this[\"market-data-aggregator\"]
```

If the old stack was never applied, skip migration and apply core then app fresh.

## Greenfield note

These stacks use new state files. Existing `lad-dev-*` AWS resources managed by the old roots are not imported automatically. If those resources still exist in the account, either import them or remove the old stacks before applying here.

## Adding environments

1. Copy `config/dev.core.backend.hcl` and `config/dev.app.backend.hcl` for the new environment (update bucket/key names).
2. Copy `../dev.tfvars` for the new environment.
3. Run bootstrap with `-var="environment=<env>"` if the bucket name differs.
4. `terraform init -reconfigure -backend-config=../config/<env>.<stack>.backend.hcl`
