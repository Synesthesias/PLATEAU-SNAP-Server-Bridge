locals {
  functions = {
    "${local.app_name}-ortho_transform" = {
      handler               = "src.ortho_transform.handler.lambda_handler"
      memory                = 512
      timeout               = 30
      needs_internet_access = false
      description           = "Handles orthographic transformations"
    },
    "${local.app_name}-roof_extraction" = {
      handler               = "src.roof_extraction.handler.lambda_handler"
      memory                = 512
      timeout               = 30
      needs_internet_access = true # only roof_extraction needs internet access
      description           = "Handles roof extraction from imagery"
    },
    "${local.app_name}-texture_building" = {
      handler               = "src.texture_building.handler.lambda_handler"
      memory                = 1024
      timeout               = 60
      needs_internet_access = false
      description           = "Builds texture for 3d models"
    }
  }

}

data "aws_ecr_image" "latest" {
  repository_name = aws_ecr_repository.geo_lambda_image.name
  most_recent = true
}

resource "aws_lambda_function" "geo_lambdas" {
  for_each = local.functions

  function_name = each.key
  architectures = ["arm64"]
  role          = aws_iam_role.lambda_exec_role.arn
  depends_on = [aws_cloudwatch_log_group.lambda_logs] # logs must exist before lambda deployment

  # Note: Docker image must be built with --provenance=false to avoid Lambda manifest compatibility issues
  package_type = "Image"
  image_uri    = "${aws_ecr_repository.geo_lambda_image.repository_url}@${data.aws_ecr_image.latest.image_digest}"
  memory_size  = each.value.memory
  timeout      = each.value.timeout
  description  = each.value.description

  image_config {
    command = [each.value.handler]
  }
  tracing_config {
    mode = "Active"
  }

  vpc_config {
    subnet_ids = [aws_subnet.private_1a.id]
    security_group_ids = [
      each.value.needs_internet_access ? aws_security_group.lambda_sg_internet_access.id : aws_security_group.lambda_sg_s3_only.id
    ]
  }

  # add env var for ALL lambdas 
  environment {
    variables = {
      OUTPUT_S3_BUCKET = aws_s3_bucket.default.bucket
    }
  }
}
resource "aws_cloudwatch_log_group" "lambda_logs" {
  for_each          = local.functions
  name              = "/aws/lambda/${each.key}"
  retention_in_days = 30
}

resource "aws_iam_role" "lambda_exec_role" {
  name = "geo_lambda_execution_role"
  assume_role_policy = jsonencode({
    Version = "2012-10-17",
    Statement = [{
      Action = "sts:AssumeRole",
      Effect = "Allow",
      Principal = {
        Service = "lambda.amazonaws.com"
      }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "lambda_basic" {
  role       = aws_iam_role.lambda_exec_role.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

resource "aws_iam_role_policy_attachment" "lambda_vpc" {
  role       = aws_iam_role.lambda_exec_role.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

resource "aws_iam_role_policy" "lambda_s3" {
  name = "lambda_s3_access"
  role = aws_iam_role.lambda_exec_role.id
  policy = jsonencode({
    Version = "2012-10-17",
    Statement = [{
      Action = ["s3:GetObject", "s3:PutObject"],
      Effect = "Allow",
      Resource = [
        aws_s3_bucket.default.arn,
        "${aws_s3_bucket.default.arn}/*"
      ]
    }]
  })
}