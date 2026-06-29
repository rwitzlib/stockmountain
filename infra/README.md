# StockMountain AWS Infrastructure

Consolidated Terraform for shared AWS resources. One state file per environment and region.

## Layout

```
infra/tf/
  bootstrap/          # S3 state bucket + DynamoDB lock table (local state)
  config/             # Backend and variable files per environment
  *.tf                # Shared platform + service resources
```

Former per-service roots under `infra/aws`, `infra/management`, `infra/aggregateception`, and `infra/kesha` are deprecated. `apps/web/tf` deploy resources live in `web-deploy.tf`.

## State backend

| Setting | Dev value |
|---------|-----------|
| Bucket | `stockmountain-dev-terraform-state-us-east-2` |
| Key | `stockmountain-dev-us-east-2.tfstate` |
| Lock table | `stockmountain-dev-terraform-locks-us-east-2` |
| Region | `us-east-2` |

## Bootstrap (first time only)

Creates the remote state bucket and lock table using local state:

```powershell
cd infra/tf/bootstrap
terraform init
terraform apply
```

## Main stack

```powershell
cd infra/tf
terraform init -backend-config=config/dev.backend.hcl
terraform plan -var-file=../dev.tfvars
terraform apply -var-file=../dev.tfvars
```

## Web app deploy (CI)

The management API deploy trigger is gated behind `enable_web_deploy`. CI should target only that resource:

```powershell
terraform apply `
  -backend-config=config/dev.backend.hcl `
  -var-file=config/dev.tfvars `
  -var="image_tag=$IMAGE_TAG" `
  -var="deploy_run_id=$GITHUB_RUN_ID" `
  -var="deploy_actor=$GITHUB_ACTOR" `
  -var="enable_web_deploy=true" `
  -target=restapi_object.web_app_deploy
```

## Greenfield note

This stack uses a new state file. Existing `lad-dev-*` AWS resources managed by the old roots are not imported automatically. If those resources still exist in the account, either import them or remove the old stacks before applying here.

## Adding environments

1. Copy `config/dev.backend.hcl` and `../dev.tfvars` for the new environment.
2. Run bootstrap with `-var="environment=<env>"` if the bucket name differs.
3. `terraform init -reconfigure -backend-config=config/<env>.backend.hcl`
