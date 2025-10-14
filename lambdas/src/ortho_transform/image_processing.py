from os import environ
from typing import Final, Tuple
import numpy as np
import cv2


from ..shared.decorators import ApiError
from .geometry_processing import ImageArray, CoordList, Dimensions, BoundingBox
from .geometry_processing import (
    _prepare_geometry_for_transform,
    _detect_optimal_corners, _sort_corners,
    _normalize_geometry_rect, _find_corner_indexes_basic,
    _safe_corr,
    _transform_points, _get_real_world_dimensions
)


BUFFER_PIXELS: Final[int] = int(environ.get("BUFFER_PIXELS", "200"))
MAX_DIMENSION: Final[int] = 2048
MIN_CONTENT_SIZE = 50
DEFAULT_BLACK_THRESHOLD = 10

from ..shared.logger import get_logger
logger = get_logger(__name__)

def process_building_extraction(
    image: ImageArray,
    pts: CoordList,
    geometry_pts: CoordList,
    has_3d_geometry: bool = False,
    buffer_pixels: int = 0
) -> Tuple[ImageArray, CoordList]:
    """Transform building image using perspective correction."""
    if len(pts) < 3:
        raise ApiError(400, "Need at least 3 coordinate points")
    if buffer_pixels < 0:
        raise ApiError(400, "Buffer pixels cannot be negative")
    try:
        logger.info(f"Processing {len(pts)} coordinate points, 3D geometry: {has_3d_geometry}")
        coordinates = np.array(pts).tolist()
        plane_geometry_pts = _prepare_geometry_for_transform(geometry_pts, has_3d_geometry)
        logger.info(f"plane_geometry_pts: {plane_geometry_pts}")

        #  safeguard (extreme outliers only)
        src = np.array(coordinates, dtype=np.float32)
        sx = float(src[:, 0].max() - src[:, 0].min()); sy = float(src[:, 1].max() - src[:, 1].min())
        src_ar = (sx / sy) if sy > 0 else 1.0

        gxy = np.array(plane_geometry_pts, dtype=np.float32)
        gx = float(gxy[:, 0].max() - gxy[:, 0].min()); gy = float(gxy[:, 1].max() - gxy[:, 1].min())
        geom_ar = (gx / gy) if gy > 0 else 1.0

        AR_TOL = 2.5    # trigger when geometry is >2.5x thinner/wider than source
        MIN_AR_ABS = 0.50   # absolute floor for geometry AR after scaling
        MAX_X_SCALE = 12.0   # allow stronger correction than 3×

        if geom_ar > 0 and (geom_ar < src_ar / AR_TOL or geom_ar < MIN_AR_ABS):
            # a reasonable target AR: <= 1.0, close to source, but with a floor
            desired_ar = max(MIN_AR_ABS, min(src_ar, 1.0))
            k_needed   = desired_ar / geom_ar
            k          = float(np.clip(k_needed, 1.0, MAX_X_SCALE))
            if abs(k - 1.0) > 1e-6:
                plane_geometry_pts = [(float(x) * k, float(y)) for (x, y) in plane_geometry_pts]
                logger.warning(
                    "Rescaled plane geometry X by %.3f (AR %.3f -> %.3f, target %.3f)",
                    k, geom_ar, geom_ar * k, desired_ar
                )
        # end safeguard


        calculated_width, image_height = _compute_output_dimensions(plane_geometry_pts, pts)

        margin = max(50, min(calculated_width, image_height) // 20)
        logger.info(f"Input: {image.shape[1]}x{image.shape[0]}px → Target: {calculated_width}x{image_height}px")

        # crop to roi
        cropped_source_image, crop_offset_1 = _crop_source_to_polygon(
            image, coordinates, buffer_pixels
        )

        # original -> cropped
        M_c1 = np.array(
            [[1, 0, -crop_offset_1[0]], [0, 1, -crop_offset_1[1]], [0, 0, 1]],
            dtype=np.float32,
        )

        coordinates_in_cropped_space = [
            (x - crop_offset_1[0], y - crop_offset_1[1]) for x, y in coordinates
        ]

        idx = _find_corner_indexes_basic(np.array(plane_geometry_pts, dtype=np.float32))
        dest_corners = np.array([plane_geometry_pts[i] for i in idx], dtype=np.float32)
        source_corners = np.array([coordinates_in_cropped_space[i] for i in idx], dtype=np.float32)

        normalized_dest = _normalize_geometry_rect(
            dest_corners.tolist(), calculated_width, image_height, margin=margin
        )

        if np.linalg.norm(dest_corners[0] - dest_corners[1]) < 1e-3 or \
        np.linalg.norm(source_corners[0] - source_corners[1]) < 1e-3:
            dest_corners = _sort_corners(_detect_optimal_corners(np.array(plane_geometry_pts, dtype=np.float32)))
            source_corners = _sort_corners(_detect_optimal_corners(np.array(coordinates_in_cropped_space, dtype=np.float32)))
            normalized_dest = _normalize_geometry_rect(
                dest_corners.tolist(), calculated_width, image_height, margin=margin
            )

        source_corners_np = source_corners.astype(np.float32)
        normalized_dest_np = np.array(normalized_dest, dtype=np.float32)

        logger.info(
            f"Source corners shape: {source_corners_np.shape}, Dest corners shape: {normalized_dest_np.shape}"
        )

        post_margin = int(buffer_pixels)

        # Create a larger canvas so the warped result has room for the buffered content
        canvas_w = int(calculated_width + 2 * post_margin)
        canvas_h = int(image_height    + 2 * post_margin)

        # Shift the destination quad into the padded canvas by the margin
        normalized_dest_np = np.array(normalized_dest, dtype=np.float32)
        normalized_dest_np += np.array([post_margin, post_margin], dtype=np.float32)

        corr_x = _safe_corr(source_corners_np[:, 0], normalized_dest_np[:, 0])
        corr_y = _safe_corr(source_corners_np[:, 1], normalized_dest_np[:, 1])

        if corr_x < 0:
            normalized_dest_np[:, 0] = (canvas_w - 1) - normalized_dest_np[:, 0]
        if corr_y < 0:
            normalized_dest_np[:, 1] = (canvas_h - 1) - normalized_dest_np[:, 1]


        # cropped -> warped
        M_w = cv2.getPerspectiveTransform(source_corners_np, normalized_dest_np)
        warped_image = cv2.warpPerspective(cropped_source_image, M_w, (canvas_w, canvas_h))


        content_bounds = _find_content_bounds(warped_image)
        
        M_orig_to_warped = M_w @ M_c1
        pts_in_warped_space = _transform_points(pts, M_orig_to_warped)
        
        coords = np.array(pts_in_warped_space)
        poly_x_min, poly_y_min = coords.min(axis=0)
        poly_x_max, poly_y_max = coords.max(axis=0)

        final_crop_x_min = max(content_bounds[0], int(poly_x_min - buffer_pixels))
        final_crop_y_min = max(content_bounds[1], int(poly_y_min - buffer_pixels))
        final_crop_x_max = min(content_bounds[2], int(poly_x_max + buffer_pixels))
        final_crop_y_max = min(content_bounds[3], int(poly_y_max + buffer_pixels))
        # Clamp
        h, w = warped_image.shape[:2]
        final_crop_x_min = max(0, final_crop_x_min)
        final_crop_y_min = max(0, final_crop_y_min)
        final_crop_x_max = min(w, final_crop_x_max)
        final_crop_y_max = min(h, final_crop_y_max)

        crop_offset_2 = (final_crop_x_min, final_crop_y_min)
        # warped -> final
        M_c2 = np.array(
            [[1, 0, -crop_offset_2[0]], [0, 1, -crop_offset_2[1]], [0, 0, 1]],
            dtype=np.float32,
        )

        final_image = warped_image[
            final_crop_y_min:final_crop_y_max, final_crop_x_min:final_crop_x_max
        ]
        M_final = M_c2 @ M_w @ M_c1
        final_points = _transform_points(pts, M_final)

        return final_image, final_points
        
    except Exception as e:
        logger.error(f"Building extraction failed: {e}")
        raise ApiError(500, f"Transformation error: {str(e)}")


def _compute_output_dimensions(geometry_pts: CoordList, coordinates_pts: CoordList) -> Dimensions:
    """
    Calculate output dimensions based on the target geometry's aspect ratio
    and the source polygon's pixel area.
    """
    real_width, real_height = _get_real_world_dimensions(geometry_pts)
    if real_width <= 0 or real_height <= 0:
        logger.warning("Invalid real-world dimensions; falling back to a 1:1 aspect ratio.")
        target_aspect_ratio = 1.0
    else:
        target_aspect_ratio = real_width / real_height

    MIN_ASPECT_RATIO = 0.5
    MAX_ASPECT_RATIO = 3.0
    
    clamped_aspect_ratio = max(MIN_ASPECT_RATIO, min(target_aspect_ratio, MAX_ASPECT_RATIO))
    if clamped_aspect_ratio != target_aspect_ratio:
        logger.warning(
            f"Original aspect ratio {target_aspect_ratio:.3f} is too extreme. "
            f"Clamping to {clamped_aspect_ratio:.3f}."
        )
        target_aspect_ratio = clamped_aspect_ratio

    source_coords = np.array(coordinates_pts)
    min_x, min_y = np.min(source_coords, axis=0)
    max_x, max_y = np.max(source_coords, axis=0)
    source_pixel_area = (max_x - min_x) * (max_y - min_y)

    if source_pixel_area <= 1:
        logger.warning("Source polygon has no area; falling back to default 512x512.")
        return (512, 512)

    calculated_height = int(np.sqrt(source_pixel_area / target_aspect_ratio))
    calculated_width = int(calculated_height * target_aspect_ratio)

    if calculated_width > MAX_DIMENSION or calculated_height > MAX_DIMENSION:
        if target_aspect_ratio > 1:  # Landscape
            calculated_width = MAX_DIMENSION
            calculated_height = int(calculated_width / target_aspect_ratio)
        else:  # Portrait or square
            calculated_height = MAX_DIMENSION
            calculated_width = int(calculated_height * target_aspect_ratio)

    MIN_DIMENSION = 100
    if calculated_width < MIN_DIMENSION or calculated_height < MIN_DIMENSION:
        if calculated_width < MIN_DIMENSION and calculated_width > 0:
             scale_factor = MIN_DIMENSION / calculated_width
             calculated_width = MIN_DIMENSION
             calculated_height = int(calculated_height * scale_factor)
        if calculated_height < MIN_DIMENSION and calculated_height > 0:
             scale_factor = MIN_DIMENSION / calculated_height
             calculated_height = MIN_DIMENSION
             calculated_width = int(calculated_width * scale_factor)

    logger.debug(
        f"Target aspect ratio: {target_aspect_ratio:.2f}. "
        f"Source area: {source_pixel_area:.0f}px. "
        f"Computed dimensions: {calculated_width}x{calculated_height}"
    )

    return max(1, calculated_width), max(1, calculated_height)


def _find_content_bounds(image: ImageArray, black_threshold: int = DEFAULT_BLACK_THRESHOLD) -> BoundingBox:
    """Find the bounding box of non-black content in the image.
    
    Returns: (x_min, y_min, x_max, y_max) where x=column, y=row
    """
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    non_black = gray > black_threshold
    rows = np.any(non_black, axis=1)
    cols = np.any(non_black, axis=0)
    
    if not np.any(rows) or not np.any(cols):
        return 0, 0, image.shape[1], image.shape[0]  # x_min, y_min, x_max, y_max
    
    y_min, y_max = np.where(rows)[0][[0, -1]]
    x_min, x_max = np.where(cols)[0][[0, -1]]
    
    return x_min, y_min, x_max + 1, y_max + 1

def _crop_source_to_polygon(image: ImageArray, polygon_coords: CoordList, buffer: int) -> Tuple[ImageArray, BoundingBox]:
    """Crop source image to polygon bounding box + buffer."""
    coords = np.array(polygon_coords)
    x_min, y_min = coords.min(axis=0).astype(int)
    x_max, y_max = coords.max(axis=0).astype(int)
    
    x_min = max(0, x_min - buffer)
    y_min = max(0, y_min - buffer) 
    x_max = min(image.shape[1], x_max + buffer)
    y_max = min(image.shape[0], y_max + buffer)
    
    cropped = image[y_min:y_max, x_min:x_max]
    crop_offset = (x_min, y_min)
    return cropped, crop_offset