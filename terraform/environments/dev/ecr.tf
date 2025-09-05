# ---------------------------------------------
# ECR
# ---------------------------------------------
resource "aws_ecr_repository" "default" {
  name = "${local.app_name}"
  tags = {
    Name = "${local.app_name}-ecr"
  }
}

resource "aws_ecr_repository" "geo_lambda_image" {
  name                 = "geo-lambda"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }
  tags = {
    Name = "${local.app_name}-ecr"
  }
}