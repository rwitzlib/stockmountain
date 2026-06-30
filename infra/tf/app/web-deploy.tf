resource "restapi_object" "web_app_deploy" {
  count = var.enable_web_deploy ? 1 : 0

  path = "/api/deploy/start"
  data = jsonencode({
    id          = var.deploy_run_id
    environment = var.environment
    repository  = local.web_service_name
    file        = "deploy.docker-compose.yml"
    image       = data.aws_ecr_image.web_app.image_uri
    actor       = var.deploy_actor
  })
}

output "web_app_deploy_response" {
  value = var.enable_web_deploy ? restapi_object.web_app_deploy[0].api_response : null
}
