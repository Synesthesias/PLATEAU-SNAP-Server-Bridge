# ---------------------------------------------
# S3 bucket
# ---------------------------------------------

resource "aws_s3_bucket" "default" {
  bucket = "${local.app_name}-bucket"

  tags = {
    Name = "${local.app_name}-bucket"
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "default" {
  bucket = aws_s3_bucket.default.id

  rule {
    id     = "delete-temp-objects"
    status = "Enabled"

    filter {
      prefix = "temp/"
    }

    expiration {
      days = 1 # 1日後に有効期限切れ
    }
  }
}
