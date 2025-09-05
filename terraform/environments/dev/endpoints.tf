resource "aws_vpc_endpoint" "s3_gateway" {
  vpc_id       = aws_vpc.default.id
  service_name = "com.amazonaws.${local.aws.region}.s3"
  vpc_endpoint_type = "Gateway"

  route_table_ids = [
    aws_route_table.private_1a.id,
  ]

  tags = {
    Name = "${local.app_name}-s3-gateway-endpoint"
  }
}