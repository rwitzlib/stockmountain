# data "aws_iam_policy_document" "kesha_assume_role" {
#   statement {
#     effect = "Allow"

#     principals {
#       type        = "Service"
#       identifiers = ["lambda.amazonaws.com"]
#     }

#     actions = ["sts:AssumeRole"]
#   }
# }

# data "aws_iam_policy" "kesha_lambda_basic_execution" {
#   arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
# }

# data "aws_iam_policy" "kesha_lambda_s3_access" {
#   arn = "arn:aws:iam::aws:policy/AWSLambdaExecute"
# }

# resource "aws_iam_role" "kesha_lambda" {
#   name               = "${local.kesha_service}Lambda"
#   assume_role_policy = data.aws_iam_policy_document.kesha_assume_role.json

#   managed_policy_arns = [
#     data.aws_iam_policy.kesha_lambda_basic_execution.arn,
#     data.aws_iam_policy.kesha_lambda_s3_access.arn
#   ]
# }

# resource "aws_lambda_function" "kesha" {
#   function_name = local.kesha_service
#   role          = aws_iam_role.kesha_lambda.arn

#   memory_size = 2048
#   timeout     = 30

#   architectures = ["x86_64"]

#   package_type = "Image"
#   image_uri    = data.aws_ecr_image.kesha.image_uri

#   environment {
#     variables = {
#       MASSIVE_TOKEN = data.aws_secretsmanager_secret_version.massive_token.secret_string
#     }
#   }
# }
