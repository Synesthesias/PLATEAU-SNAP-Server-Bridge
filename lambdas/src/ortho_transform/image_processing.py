from os import environ
from typing import Final
import numpy as np
import cv2


from ..shared.decorators import ApiError
from .geometry_processing import CoordList, Dimensions
from .geometry_processing import (
    _geometry_to_y_down_plane_coords,
    _index_corners_tl_tr_br_bl,
    _safe_corr,
    _apply_homography_to_points, _compute_axis_aligned_size_2d
)


BUFFER_PIXELS: Final[int] = int(environ.get("BUFFER_PIXELS", "200"))
MAX_DIMENSION: Final[int] = 4096

from ..shared.logger import get_logger
logger = get_logger(__name__)

def rectify_facade(image, pts, geometry_pts, facade_buffer_px=BUFFER_PIXELS):
    if len(pts) < 3 or len(geometry_pts) < 3:
        logger.warning("rectify_facade: insufficient correspondences (pts=%d, geometry_pts=%d)", len(pts), len(geometry_pts))
        raise ApiError(400, "Need ≥3 correspondences")
    
    logger.debug("rectify_facade: start (pts=%d, geometry_pts=%d, buffer=%d)",
                 len(pts), len(geometry_pts), int(facade_buffer_px))
    num_original_pts = len(pts)

    # Plane coords (Y-down to match image)
    plane2d = np.asarray(_geometry_to_y_down_plane_coords(geometry_pts), np.float32)
    img2d   = np.asarray(pts, np.float32)
    
    # If 3 points, synthesize a 4th to form a square
    if len(plane2d) == 3 and len(img2d) == 3:
        logger.debug("rectify_facade: synthesizing 4th point from 3 triangle points")
        plane2d = _get_bounding_box_from_triangle(plane2d)
        img2d = _get_bounding_box_from_triangle(img2d)
    
    # Pick pairs (all if lengths match, else 4 corners in TL,TR,BR,BL)
    use_all = (len(plane2d) == len(img2d) and len(plane2d) >= 6)
    if use_all:
        g2, i2 = plane2d, img2d
    else:
        idx4 = _index_corners_tl_tr_br_bl(plane2d)
        g2, i2 = plane2d[idx4], img2d[idx4]

    # Homography plane→image
    H_g2i, _ = cv2.findHomography(g2, i2, cv2.RANSAC, 13.0, maxIters=4000, confidence=0.999)
    if H_g2i is None:
        logger.error("rectify_facade: findHomography failed (pairs=%d)", len(g2))
        raise ApiError(500, "findHomography failed")

    # Check if H_g2i is invertible
    det = np.linalg.det(H_g2i)
    if abs(det) < 1e-6:
        logger.error("rectify_facade: homography is singular (det=%.2e)", det)
        raise ApiError(400, "Homography is singular; insufficient point correspondence")
    
    # Output size & margins
    W, H = _compute_output_canvas_from_aspect_and_bbox_area(plane2d.tolist(), pts)
    m = max(50, min(W, H)//20)
    logger.debug("rectify_facade: output size W=%d H=%d margin=%d", W, H, m)

    # Plane bbox (TL,TR,BR,BL), expanded by facade_buffer_px mapped into plane units
    idx4_all = _index_corners_tl_tr_br_bl(plane2d)
    P = plane2d[idx4_all].astype(np.float32)
    pmin, pmax = P.min(0), P.max(0)
    pw, ph = float(pmax[0]-pmin[0]), float(pmax[1]-pmin[1])
    iw, ih = float(W-2*m), float(H-2*m)

    if pw>1e-6 and ph>1e-6 and iw>1e-6 and ih>1e-6 and facade_buffer_px>0:
        bx = min((facade_buffer_px*pw)/iw, 0.25*pw)
        by = min((facade_buffer_px*ph)/ih, 0.25*ph)
        pmin -= (bx, by); pmax += (bx, by)

    P_exp = np.array([[pmin[0],pmin[1]],[pmax[0],pmin[1]],[pmax[0],pmax[1]],[pmin[0],pmax[1]]], np.float32)
    D = np.array([[m,m],[W-m,m],[W-m,H-m],[m,H-m]], np.float32)

    H_g2d = cv2.getPerspectiveTransform(P_exp, D)
    try:
        H_i2d = H_g2d @ np.linalg.inv(H_g2i)
    except np.linalg.LinAlgError as e:
        logger.error("rectify_facade: failed to invert homography: %s", str(e))
        raise ApiError(500, "Failed to invert homography matrix")
    
    H_i2d = _fix_axis_flips_by_src_dst_correlation(H_i2d, H_g2i, H_g2d, P_exp, W, H)

    warped = cv2.warpPerspective(image, H_i2d, (W, H))
    warped_pts = _apply_homography_to_points(pts, H_i2d)
    warped_pts = warped_pts[:num_original_pts]
    logger.debug("rectify_facade: warp complete; transformed %d points", len(warped_pts))
    return warped, [(float(x), float(y)) for x,y in warped_pts]


def _compute_output_canvas_from_aspect_and_bbox_area(geometry_pts: CoordList, coordinates_pts: CoordList) -> Dimensions:
    """
    Calculate output dimensions based on the target geometry's aspect ratio
    and the source polygon's pixel area.
    """
    real_width, real_height = _compute_axis_aligned_size_2d(geometry_pts)
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


def _fix_axis_flips_by_src_dst_correlation(H_i2d, H_g2i, H_g2d, plane_quad, W, H):
    """Apply horizontal and vertical flip corrections if needed."""
    src_x = (H_g2i @ np.column_stack([plane_quad, np.ones(4)]).T)
    src_x = (src_x[0] / src_x[2]).ravel()
    dst_x = (H_g2d @ np.column_stack([plane_quad, np.ones(4)]).T)
    dst_x = (dst_x[0] / dst_x[2]).ravel()
    
    corr_x = _safe_corr(src_x, dst_x)
    if corr_x < 0:
        Fh = np.array([[-1,0,W-1],[0,1,0],[0,0,1]], dtype=np.float32)
        H_i2d = Fh @ H_i2d
        logger.info("Applied HORIZONTAL flip (corr_x=%.4f)", float(corr_x))


    src_y = (H_g2i @ np.column_stack([plane_quad, np.ones(4)]).T)
    src_y = (src_y[1] / src_y[2]).ravel()
    dst_y = (H_g2d @ np.column_stack([plane_quad, np.ones(4)]).T)
    dst_y = (dst_y[1] / dst_y[2]).ravel()
    
    corr_y = _safe_corr(src_y, dst_y)
    if corr_y < 0:
        Fv = np.array([[1,0,0],[0,-1,H-1],[0,0,1]], dtype=np.float32)
        H_i2d = Fv @ H_i2d
        logger.info("Applied VERTICAL flip (corr_y=%.4f)", float(corr_y))
    
    return H_i2d

def _get_bounding_box_from_triangle(pts_3):
    """
    Given 3 points forming a triangle, find the bounding rect.
    Returns box points in canonical TL, TR, BR, BL order.
    """
    pts_array = np.array(pts_3, dtype=np.float32)
    rect = cv2.minAreaRect(pts_array)
    box = cv2.boxPoints(rect)
    
    # Reorder to TL, TR, BR, BL
    x_min, y_min = box.min(axis=0)
    x_max, y_max = box.max(axis=0)
    
    dists_tl = np.sum((box - (x_min, y_min)) ** 2, axis=1)
    dists_tr = np.sum((box - (x_max, y_min)) ** 2, axis=1)
    dists_br = np.sum((box - (x_max, y_max)) ** 2, axis=1)
    dists_bl = np.sum((box - (x_min, y_max)) ** 2, axis=1)
    
    tl = np.argmin(dists_tl)
    tr = np.argmin(dists_tr)
    br = np.argmin(dists_br)
    bl = np.argmin(dists_bl)
    
    ordered_box = np.array([box[tl], box[tr], box[br], box[bl]], dtype=np.float32)
    return ordered_box