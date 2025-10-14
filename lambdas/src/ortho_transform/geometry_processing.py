from typing import List, Tuple, Dict
import numpy as np
import numpy.typing as npt
import cv2
from shapely import wkt

from ..shared.decorators import ApiError

from ..shared.logger import get_logger
logger = get_logger(__name__)

MIN_CORNER_DISTANCE = 2.0
# Coordinate
Coord = Tuple[float, float]
CoordList = List[Coord]
Coord3D = Tuple[float, float, float]
CoordList3D = List[Coord3D]

ImageArray = npt.NDArray[np.uint8]
FloatArray = npt.NDArray[np.float32]
IntArray = npt.NDArray[np.int32]

# Geometry
BoundingBox = Tuple[int, int, int, int] # (x_min, y_min, x_max, y_max)
Dimensions = Tuple[int, int]
TransformMatrix = npt.NDArray[np.float32]
RectangleCorners = CoordList

# Quality assessment
QualityMetrics = Dict[str, float]
CornerMethod = Tuple[str, FloatArray, float]  # method_name, corners, score


def parse_wkt_polygon(wkt_string: str) -> CoordList:
    """Parse WKT polygon string to extract coordinates."""
    polygon = wkt.loads(wkt_string)
    coords = list(polygon.exterior.coords)
    return coords[:-1]  # Remove duplicate closing coordinate

def _get_real_world_dimensions(plane_geometry_pts: CoordList) -> Tuple[float, float]:
    """
    Calculate real-world dimensions from a list of **2D plane** geometry coordinates.
    Returns (width, height). Returns (0.0, 0.0) on failure.
    """
    if not plane_geometry_pts:
        return 0.0, 0.0

    try:
        coords = np.array(plane_geometry_pts)
        xs = coords[:, 0]
        ys = coords[:, 1]
        width, height = np.max(xs) - np.min(xs), np.max(ys) - np.min(ys)
        logger.debug(f"Using 2D geometry for dimensions: {width:.2f}x{height:.2f}")

        return (width, height) if width > 0 and height > 0 else (0.0, 0.0)

    except (IndexError, TypeError) as e:
        logger.warning(f"Failed to parse geometry for dimensions: {e}. Cannot determine aspect ratio.")
        return 0.0, 0.0


def _prepare_geometry_for_transform(geometry_pts: CoordList, has_3d: bool = False) -> List[List[float]]:
    """Process geometry coordinates, handling 3D projection if needed."""
    if has_3d and len(geometry_pts) > 0 and len(geometry_pts[0]) >= 3:
        points_3d = np.array(geometry_pts, dtype=np.float32)
        normalized_coords = _project_facade_to_2d(points_3d)
        max_y = np.max(normalized_coords[:, 1])
        geometry = [[pt[0], max_y - pt[1]] for pt in normalized_coords]
    else:
        geometry_coords = np.array(geometry_pts)
        max_y = geometry_coords[:, 1].max()
        geometry = [[point[0], max_y - point[1]] for point in geometry_coords]
    
    return geometry


def _project_facade_to_2d(points_3d: np.ndarray) -> np.ndarray:
    """
    Projects 3D points to the best-fitting facade plane using PCA,
    and normalizes them to a (0,0) origin.
    Uses Z-axis (height) to determine proper orientation.
    """
    if len(points_3d) < 3:
        logger.warning(
            "Need at least 3 points to define a plane. Using XZ projection as fallback."
        )
        facade_coords = points_3d[:, [0, 2]]
        min_vals = np.min(facade_coords, axis=0)
        return facade_coords - min_vals

    # Calculate the centroid and center the points
    centroid = np.mean(points_3d, axis=0)
    centered_points = points_3d - centroid
    logger.debug(f"Original points shape: {points_3d.shape}, centroid: {centroid}")

    # Analyze Z variation (height)
    z_coords = points_3d[:, 2]
    z_min, z_max = np.min(z_coords), np.max(z_coords)
    height_variation = z_max - z_min
    logger.debug(
        f"Z-axis range: [{z_min:.2f}, {z_max:.2f}], height variation: {height_variation:.2f}"
    )

    # PCA
    covariance_matrix = np.cov(centered_points.T)
    eigenvalues, eigenvectors = np.linalg.eigh(covariance_matrix)
    sorted_indices = np.argsort(eigenvalues)[::-1]
    sorted_eigenvalues = eigenvalues[sorted_indices]
    sorted_eigenvectors = eigenvectors[:, sorted_indices]

    # Plane normal is the smallest eigenvector
    plane_normal = sorted_eigenvectors[:, 2]
    logger.debug(f"Eigenvalues (desc): {sorted_eigenvalues}")
    logger.debug(f"Plane normal: {plane_normal}")

    # Decide facade type
    vertical_component = abs(plane_normal[2])  # Z component
    is_vertical_facade = height_variation > 1.0 and vertical_component < 0.7

    if is_vertical_facade:
        # Vertical facade: horizontal axis from primary component projected to XY,
        # vertical axis aligned with world Z.
        primary_component = sorted_eigenvectors[:, 0]
        horizontal_component = np.array([primary_component[0], primary_component[1], 0])

        if np.linalg.norm(horizontal_component) < 1e-6:
            horizontal_component = np.array([1, 0, 0])
        else:
            horizontal_component = horizontal_component / np.linalg.norm(
                horizontal_component
            )

        u = horizontal_component                    # horizontal
        v = np.array([0, 0, 1])                     # vertical (world Z)
    else:
        # Horizontal surface: use top two principal components
        u = sorted_eigenvectors[:, 0]
        v = sorted_eigenvectors[:, 1]

    # Orthonormalize
    u = u / np.linalg.norm(u)
    v = v / np.linalg.norm(v)

    if is_vertical_facade:
        # Ensure v ⟂ u
        v = v - np.dot(v, u) * u
        v = v / np.linalg.norm(v)
    else:
        # Ensure right-handed system for horizontal surfaces
        cross_product = np.cross(u, v)
        if np.dot(cross_product, plane_normal) < 0:
            u = -u

    facade_type = "vertical" if is_vertical_facade else "horizontal"
    logger.info(f"Facade type: {facade_type}, height variation: {height_variation:.2f}")
    logger.info(f"Projecting to facade plane with normal: {plane_normal}")
    logger.debug(f"Final basis vectors - u (horizontal): {u}, v (vertical): {v}")

    # Project to 2D and normalize to (0,0)
    projected_2d = np.column_stack(
        [np.dot(centered_points, u), np.dot(centered_points, v)]
    )
    min_vals = np.min(projected_2d, axis=0)
    normalized_coords = projected_2d - min_vals

    final_width = np.max(normalized_coords[:, 0])
    final_height = np.max(normalized_coords[:, 1])
    logger.debug(f"Final normalized dimensions: {final_width:.2f} x {final_height:.2f}")

    return normalized_coords


def _normalize_geometry_rect(rectangle_points: RectangleCorners, image_width: int, image_height: int, margin: int = 100) -> RectangleCorners:
    """
    Normalize a 4-point rectangle to fit within a specified image size.
    Assumes `rectangle_points` contains exactly 4 valid corner points.
    """
    if len(rectangle_points) != 4:
        raise ValueError(f"Expected 4 corner points for normalization, but got {len(rectangle_points)}")

    points_arr = np.array(rectangle_points, dtype=np.float32)
    x_coords, y_coords = points_arr[:, 0], points_arr[:, 1]
    min_x, max_x = np.min(x_coords), np.max(x_coords)
    min_y, max_y = np.min(y_coords), np.max(y_coords)
    geometry_width, geometry_height = max_x - min_x, max_y - min_y

    # protect against collapsed area
    MIN_EFFECTIVE = 32
    eff_w = image_width - 2 * margin
    eff_h = image_height - 2 * margin
    if eff_w < MIN_EFFECTIVE or eff_h < MIN_EFFECTIVE:
        max_margin_w = (image_width  - MIN_EFFECTIVE) // 2
        max_margin_h = (image_height - MIN_EFFECTIVE) // 2
        margin = max(0, min(margin, max_margin_w, max_margin_h))
        eff_w = image_width - 2 * margin
        eff_h = image_height - 2 * margin

    if geometry_width <= 0 or geometry_height <= 0:
        logger.warning(f"Invalid geometry dimensions (width={geometry_width}, height={geometry_height}). Returning default rectangle.")
        return [[margin, margin], [image_width - margin, margin], [image_width - margin, image_height - margin], [margin, image_height - margin]]

    scale = min(eff_w / geometry_width, eff_h / geometry_height)
    offset_x = margin + (eff_w - geometry_width * scale) / 2
    offset_y = margin + (eff_h - geometry_height * scale) / 2

    return [
        ((pt[0] - min_x) * scale + offset_x, (pt[1] - min_y) * scale + offset_y)
        for pt in rectangle_points
    ]


def _validate_corner_count(corners: np.ndarray) -> np.ndarray:
    """Ensure we have exactly 4 corner points, removing duplicates if needed."""
    corners_float = corners.astype(np.float32)
    
    # Remove duplicate last point if it matches first
    if corners_float.shape[0] == 5 and np.array_equal(corners_float[0], corners_float[4]):
        corners_float = corners_float[:-1]
    
    if corners_float.shape != (4, 2):
        raise ApiError(400, f"Need exactly 4 corners, got {corners_float.shape}")
    
    return corners_float


def _transform_points(points: CoordList, matrix: np.ndarray) -> CoordList:
    """Transform a list of points using the given transformation matrix."""
    points_array = np.array(points, dtype=np.float32)
    ones = np.ones((len(points_array), 1))
    homogeneous_points = np.hstack([points_array, ones])
    
    transformed_homogeneous = matrix @ homogeneous_points.T
    transformed_points = (transformed_homogeneous[:2] / transformed_homogeneous[2]).T
    
    return [(float(x), float(y)) for x, y in transformed_points]


def _extract_rectangle_corners(points) -> np.ndarray:
    """Create rectangle using advanced corner detection algorithm."""
    unique_points = []
    for point in points:
        is_duplicate = any(np.allclose(point, up) for up in unique_points)
        if not is_duplicate:
             unique_points.append(point)
    
    if len(unique_points) < 4:
        raise ValueError("Need at least 4 unique points for rectangle creation")
    
    points_arr = np.array(unique_points, dtype=np.float32)

    if _is_concave(points_arr):
        logger.info("Concave polygon detected. Using convex hull for corner detection.")
        hull = cv2.convexHull(points_arr.reshape(-1, 1, 2), returnPoints=True)
        corners = _detect_optimal_corners(hull.squeeze(axis=1))
    else:
        corners = _detect_optimal_corners(points_arr)

    return corners

def _is_concave(points: np.ndarray) -> bool:
    """Check if a polygon is concave."""
    if len(points) <= 3:
        return False
    hull = cv2.convexHull(points.reshape(-1, 1, 2), returnPoints=True)
    return len(hull) < len(points)

def _extract_basic_rectangle_corners(points) -> np.ndarray:
    """Create rectangle using basic corner detection algorithm."""
    unique_points = []
    for point in points:
        if not unique_points or point != unique_points[0]:
            unique_points.append(point)
    
    if len(unique_points) < 4:
        raise ValueError("Need at least 4 unique points for rectangle creation")
    
    corners = _find_corners_basic(np.array(unique_points))
    return corners


def _point_to_line_segment_distance(point, line_start, line_end):
    """Calculate distance from point to line segment."""
    line_vec = line_end - line_start
    point_vec = point - line_start
    line_len = np.linalg.norm(line_vec)
    if line_len == 0:
        return np.linalg.norm(point_vec)

    t = max(0, min(1, np.dot(point_vec, line_vec) / (line_len**2)))
    projection = line_start + t * line_vec
    return np.linalg.norm(point - projection)

def _assess_corner_quality(points: FloatArray, corners: FloatArray) -> QualityMetrics:
    """Assess quality of detected corners to determine if method failed."""
    corner_distances = []
    for i in range(4):
        dist = np.linalg.norm(corners[i] - corners[(i+1) % 4])
        corner_distances.append(dist)

    min_side = min(corner_distances)
    max_side = max(corner_distances)
    side_ratio = max_side / min_side if min_side > 0 else float('inf')

    angles = []
    for i in range(4):
        p1 = corners[(i-1) % 4]
        p2 = corners[i]
        p3 = corners[(i+1) % 4]

        v1 = p1 - p2
        v2 = p3 - p2

        if np.linalg.norm(v1) > 0 and np.linalg.norm(v2) > 0:
            cos_angle = np.dot(v1, v2) / (np.linalg.norm(v1) * np.linalg.norm(v2))
            angle = np.degrees(np.arccos(np.clip(cos_angle, -1, 1)))
            angles.append(angle)

    angle_deviation = np.std(angles) if angles else float('inf')

    total_deviation = 0
    for point in points:
        min_dist_to_quad = float('inf')
        for i in range(4):
            edge_start = corners[i]
            edge_end = corners[(i+1) % 4]
            dist = _point_to_line_segment_distance(point, edge_start, edge_end)
            min_dist_to_quad = min(min_dist_to_quad, dist)
        total_deviation += min_dist_to_quad

    avg_deviation = total_deviation / len(points)

    corner_proximity_scores = []
    for corner in corners:
        distances_to_points = np.sum((points - corner)**2, axis=1)
        min_distance = np.sqrt(np.min(distances_to_points))
        corner_proximity_scores.append(min_distance)

    max_corner_distance = max(corner_proximity_scores)

    return {
        'side_ratio': side_ratio,
        'angle_deviation': angle_deviation,
        'avg_point_deviation': avg_deviation,
        'max_corner_distance': max_corner_distance,
        'min_side_length': min_side
    }

def _score_corner_quality(quality_metrics: dict) -> float:
    """Score corner detection quality. Higher score = better."""
    if (quality_metrics['min_side_length'] < 10 or
        quality_metrics['side_ratio'] == float('inf') or
        quality_metrics['max_corner_distance'] > 100):
        return -1000
    
    score = 100
    score -= quality_metrics['avg_point_deviation'] * 2
    
    side_ratio = quality_metrics['side_ratio']
    if side_ratio > 4:
        score -= (side_ratio - 4) * 20
    elif side_ratio > 2.5:
        score -= (side_ratio - 2.5) * 10
    
    angle_dev = quality_metrics['angle_deviation']
    if angle_dev > 30:
        score -= (angle_dev - 30) * 1.5
    elif angle_dev > 15:
        score -= (angle_dev - 15) * 0.5
    
    score -= quality_metrics['max_corner_distance'] * 0.1
    
    if quality_metrics['min_side_length'] > 200:
        score += 10
    
    return score

def _sort_corners(corners: np.ndarray) -> np.ndarray:
    """
    Orders 4 corners in a consistent clockwise direction, starting from
    the one closest to the top-left of the image's bounding box.
    """
    center = corners.mean(axis=0)
    
    angles = np.arctan2(corners[:, 1] - center[1], corners[:, 0] - center[0])
    
    sorted_indices = np.argsort(angles)
    corners = corners[sorted_indices]

    return np.array(list(corners), dtype=np.float32)

def _remove_collinear_points(points: np.ndarray, min_distance: float = 1.0) -> np.ndarray:
    """Remove collinear points using perpendicular distance threshold"""
    if len(points) < 4:
        return points
    
    cleaned = [points[0]]
    
    for i in range(1, len(points) - 1):
        p1 = points[i - 1]
        p2 = points[i]
        p3 = points[i + 1]
        
        line_vec = p3 - p1
        norm = np.linalg.norm(line_vec)
        
        if norm == 0:
            continue
            
        point_vec = p1 - p2
        distance = np.abs(line_vec[0] * point_vec[1] - line_vec[1] * point_vec[0]) / norm
        
        if distance > min_distance:
            cleaned.append(points[i])
    
    cleaned.append(points[-1])
    
    if len(cleaned) > 3:
        p1 = cleaned[-2]
        p2 = cleaned[-1] 
        p3 = cleaned[0]
        
        line_vec = p3 - p1
        norm = np.linalg.norm(line_vec)
        if norm > 0:
            point_vec = p1 - p2
            distance = np.abs(line_vec[0] * point_vec[1] - line_vec[1] * point_vec[0]) / norm
            if distance < min_distance:
                cleaned.pop()
    
    return np.array(cleaned) if len(cleaned) >= 3 else points

def _find_corners_adaptive_approx(points: np.ndarray) -> np.ndarray:
    """Use adaptive Douglas-Peucker approximation to find 4 corners"""
    poly = points.astype(np.int32).reshape(-1, 1, 2)
    arc_length = cv2.arcLength(poly, closed=True)
    
    for epsilon_factor in np.logspace(0, -3, num=50):
        epsilon = epsilon_factor * arc_length
        approx = cv2.approxPolyDP(poly, epsilon, closed=True)
        
        if len(approx) == 4:
            corners = approx.squeeze(axis=1).astype(np.float32)
            return _sort_corners(corners)

    rect = cv2.minAreaRect(poly)
    corners = cv2.boxPoints(rect)
    return _sort_corners(corners)


def _find_corners_oriented_bbox(points: np.ndarray) -> np.ndarray:
    """Find corners using PCA-oriented bounding box"""
    # Manual PCA
    centered = points - np.mean(points, axis=0)
    cov_matrix = np.cov(centered.T)
    eigenvalues, eigenvectors = np.linalg.eigh(cov_matrix)

    idx = np.argsort(eigenvalues)[::-1]
    components = eigenvectors[:, idx].T

    points_aligned = centered @ components.T

    min_x, min_y = points_aligned.min(axis=0)
    max_x, max_y = points_aligned.max(axis=0)

    aligned_corners = np.array([
        [min_x, min_y], [max_x, min_y],
        [max_x, max_y], [min_x, max_y]
    ])
    
    corners = aligned_corners @ components + np.mean(points, axis=0)
    
    actual_corners = []
    for corner in corners:
        distances = np.sum((points - corner)**2, axis=1)
        actual_corners.append(points[np.argmin(distances)])

    return _sort_corners(np.array(actual_corners, dtype=np.float32))


def _find_corners_basic(points: np.ndarray) -> np.ndarray:
    """Basic corner finding using min/max coordinates"""
    x_min, y_min = points.min(axis=0)
    x_max, y_max = points.max(axis=0)
    dists_tl = np.sum((points - (x_min, y_min))**2, axis=1)
    dists_tr = np.sum((points - (x_max, y_min))**2, axis=1)
    dists_br = np.sum((points - (x_max, y_max))**2, axis=1)
    dists_bl = np.sum((points - (x_min, y_max))**2, axis=1)
    tl = points[np.argmin(dists_tl)]
    tr = points[np.argmin(dists_tr)]
    br = points[np.argmin(dists_br)]
    bl = points[np.argmin(dists_bl)]
    return np.array([tl, tr, br, bl], dtype=np.float32)

def _detect_optimal_corners(points: np.ndarray) -> np.ndarray:
    """Try multiple methods and pick the one with the best quality score."""
    cleaned_points = _remove_collinear_points(points, min_distance=MIN_CORNER_DISTANCE)

    methods = [
        ('adaptive_approx', _find_corners_adaptive_approx),
        ('oriented_bbox', _find_corners_oriented_bbox),
        ('basic', _find_corners_basic)
    ]
    
    results = []
    
    for method_name, method_func in methods:
        try:
            corners = method_func(cleaned_points)
            quality = _assess_corner_quality(cleaned_points, corners)
            score = _score_corner_quality(quality)
            results.append((method_name, corners, score))
        except Exception:
            continue
    
    if not results:
        logger.warning(f"All corner detection methods failed on {len(points)} points")
        return _find_corners_basic(points)
    
    results.sort(key=lambda x: x[2], reverse=True)
    best_method, best_corners, best_score = results[0]
    
    if best_score < 0:
        logger.warning("Best corner detection method (%s) has poor quality score: %.1f", best_method, best_score)
    
    logger.debug(f"Best method: {best_method} (score: {best_score:.1f})")
    return best_corners

def _find_corner_indexes_basic(points: np.ndarray) -> np.ndarray:
    """Return indexes of TL, TR, BR, BL using min/max coordinate anchors."""
    x_min, y_min = points.min(axis=0)
    x_max, y_max = points.max(axis=0)
    dists_tl = np.sum((points - (x_min, y_min)) ** 2, axis=1)
    dists_tr = np.sum((points - (x_max, y_min)) ** 2, axis=1)
    dists_br = np.sum((points - (x_max, y_max)) ** 2, axis=1)
    dists_bl = np.sum((points - (x_min, y_max)) ** 2, axis=1)
    tl = np.argmin(dists_tl)
    tr = np.argmin(dists_tr)
    br = np.argmin(dists_br)
    bl = np.argmin(dists_bl)
    return np.array([tl, tr, br, bl], dtype=np.int32)

def _safe_corr(a: np.ndarray, b: np.ndarray) -> float:
    """Pearson corr; returns 0 if either is (near) constant to avoid NaNs."""
    a = np.asarray(a, dtype=np.float32).ravel()
    b = np.asarray(b, dtype=np.float32).ravel()
    if a.size != b.size or a.size < 2:
        return 0.0
    if a.std() < 1e-6 or b.std() < 1e-6:
        return 0.0
    return float(np.corrcoef(a, b)[0, 1])
