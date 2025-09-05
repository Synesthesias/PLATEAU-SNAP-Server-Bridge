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

# Quality assessment
QualityMetrics = Dict[str, float]
CornerMethod = Tuple[str, FloatArray, float]  # method_name, corners, score


def parse_wkt_polygon(wkt_string: str) -> CoordList:
    """Parse WKT polygon string to extract coordinates."""
    polygon = wkt.loads(wkt_string)
    coords = list(polygon.exterior.coords)
    return coords[:-1]  # Remove duplicate closing coordinate

def _get_real_world_dimensions(geometry_pts: CoordList) -> Tuple[float, float]:
    """
    Calculate real-world dimensions from a list of geometry coordinates.
    Returns (width, height). Returns (0.0, 0.0) on failure.
    """
    if not geometry_pts:
        return 0.0, 0.0

    try:
        coords = np.array(geometry_pts)
        # 3D
        if coords.shape[1] >= 3:
            xs = coords[:, 0]
            zs = coords[:, 2]
            width, height = np.max(xs) - np.min(xs), np.max(zs) - np.min(zs)
            logger.debug(f"Using 3D geometry for dimensions: {width:.2f}x{height:.2f}")
        # 2d fallback
        else:
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
    if has_3d and len(geometry_pts[0]) >= 3:
        points_3d = np.array(geometry_pts)
        yz_coords = _project_to_yz_plane_front_view(points_3d)
        transformed_coords, origin = _normalize_coordinates_from_origin(yz_coords)
        
        max_y = transformed_coords[:, 1].max()
        geometry = [[point[0], max_y - point[1]] for point in transformed_coords]
    else:
        geometry_coords = np.array(geometry_pts)
        max_y = geometry_coords[:, 1].max()
        geometry = [[point[0], max_y - point[1]] for point in geometry_coords]
    
    return geometry


def _project_to_yz_plane_front_view(points_3d):
    """Project 3D coordinates to YZ plane front view"""
    yz_coords = points_3d[:, [1, 2]]
    front_view_coords = yz_coords.copy()
    front_view_coords[:, 0] = -front_view_coords[:, 0]
    return front_view_coords

def _normalize_coordinates_from_origin(coords_2d):
    """Transform coordinates with bottom-left vertex as origin (0,0)"""
    transformed_coords = coords_2d.copy()
    
    x_min = coords_2d[:, 0].min()
    y_min = coords_2d[:, 1].min()
    
    transformed_coords[:, 0] = coords_2d[:, 0] - x_min
    transformed_coords[:, 1] = coords_2d[:, 1] - y_min
    
    return transformed_coords, (x_min, y_min)


def _normalize_geometry_rect(rectangle_points, image_width, image_height, margin=100):
    """Normalize geometry rectangle to fit within specified image size"""
    if not rectangle_points:
        return []
    
    unique_points = []
    for point in rectangle_points:
        if not unique_points or point != unique_points[0]:
            unique_points.append(point)
    
    if len(unique_points) < 4:
        return []
    
    x_coords = [point[0] for point in unique_points]
    y_coords = [point[1] for point in unique_points]
    
    min_x, max_x = min(x_coords), max(x_coords)
    min_y, max_y = min(y_coords), max(y_coords)
    
    geometry_width = max_x - min_x
    geometry_height = max_y - min_y
    
    effective_width = image_width - 2 * margin
    effective_height = image_height - 2 * margin
    
    scale_x = effective_width / geometry_width if geometry_width != 0 else 1
    scale_y = effective_height / geometry_height if geometry_height != 0 else 1
    
    scale = min(scale_x, scale_y)
    
    normalized_rect = []
    for point in unique_points:
        norm_x = (point[0] - min_x) * scale
        norm_y = (point[1] - min_y) * scale
        
        final_x = norm_x + margin + (effective_width - geometry_width * scale) / 2
        final_y = norm_y + margin + (effective_height - geometry_height * scale) / 2
        
        normalized_rect.append([final_x, final_y])

    return normalized_rect


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
        if not unique_points or point != unique_points[0]:
            unique_points.append(point)
    
    if len(unique_points) < 4:
        raise ValueError("Need at least 4 unique points for rectangle creation")
    
    corners = _detect_optimal_corners(np.array(unique_points))
    return corners

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
    """Orders 4 corners: top-left, top-right, bottom-right, bottom-left"""
    coord_sum = corners.sum(axis=1)
    tl = corners[np.argmin(coord_sum)]
    br = corners[np.argmax(coord_sum)]
    
    coord_diff = np.diff(corners, axis=1).flatten()
    tr = corners[np.argmin(coord_diff)]
    bl = corners[np.argmax(coord_diff)]
    
    return np.array([tl, tr, br, bl], dtype=np.float32)

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

