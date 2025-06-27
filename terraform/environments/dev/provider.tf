terraform {
  required_version = ">= 1.7"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = ">= 3.0"
    }
  }
  backend "s3" {
    region  = "<Terraform Backend Region>"
    bucket  = "<Terraform Backend Bucket>"
    key     = "<Terraform Backend Path To the State File>"
    encrypt = true
  }
}

provider "aws" {
  region     = local.aws.region
}
