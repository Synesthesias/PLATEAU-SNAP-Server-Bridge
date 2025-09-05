from __future__ import annotations

from urllib.parse import urlparse, ParseResult

import boto3
import cv2
import numpy as np
from botocore.exceptions import ClientError
from typing import NoReturn

from ..shared.decorators import ApiError

S3 = boto3.client("s3")

def _parse_s3_uri(s3_uri: str) -> ParseResult:
    if not s3_uri.lower().startswith("s3://"):
        s3_uri = f"s3://{s3_uri}"

    parsed = urlparse(s3_uri)
    if parsed.scheme.lower() != "s3":
        raise ApiError(400, f"Invalid S3 URI scheme: '{parsed.scheme}'")
    if not parsed.netloc:
        raise ApiError(400, "S3 URI must include a bucket name")

    return parsed


def download_from_s3(s3_uri: str) -> np.ndarray:
    p = _parse_s3_uri(s3_uri)
    # no encoded chars (%20, +) so no need for unquote_plus. 
    bucket = p.netloc
    key = p.path.lstrip("/")

    try:
        resp = S3.get_object(Bucket=bucket, Key=key)
    except ClientError as err:
        _handle_s3_client_error(err, bucket, key)
    arr = np.frombuffer(resp["Body"].read(), np.uint8)
    img = cv2.imdecode(arr, cv2.IMREAD_COLOR)
    if img is None:
        raise ApiError(422, f"Failed to decode image data from {s3_uri}")
    return img


def upload_png_to_s3(img: np.ndarray, bucket: str, key: str) -> str:
    ok, buf = cv2.imencode(".png", img)
    if not ok:
        raise ApiError(422, "PNG encoding failed")

    try:
        S3.put_object(
            Bucket=bucket,
            Key=key,
            Body=buf.tobytes(),
            ContentType="image/png",
        )
    except ClientError as err:
        _handle_s3_client_error(err, bucket, key)
    return f"S3://{bucket}/{key}"

def _handle_s3_client_error(err: ClientError, bucket: str, key: str) -> NoReturn:
    """
    Translate a botocore ClientError into an ApiError.

    * NoSuchKey      -> 404  Not Found
    * AccessDenied   -> 403  Forbidden
    * All others     -> 502  Bad Gateway
    """
    error_code = err.response.get("Error", {}).get("Code", "")
    if error_code == "NoSuchKey":
        raise ApiError(404, f"Object '{key}' not found in bucket '{bucket}'") from err
    if error_code == "AccessDenied":
        raise ApiError(403, f"Access denied to s3://{bucket}/{key}") from err
    raise ApiError(502, f"S3 error ({error_code}): {err}") from err