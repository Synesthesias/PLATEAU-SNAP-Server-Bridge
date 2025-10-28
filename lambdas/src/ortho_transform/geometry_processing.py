from typing import List, Tuple
import numpy as np
from shapely import wkt

from ..shared.logger import get_logger
logger = get_logger(__name__)

# Coordinate
Coord = Tuple[float, float]
CoordList = List[Coord]

# Geometry
Dimensions = Tuple[int, int]


def wkt_polygon_to_coords(wkt_string: str) -> CoordList:
    """Parse WKT polygon string to extract coordinates."""
    polygon = wkt.loads(wkt_string)
    coords = list(polygon.exterior.coords)
    return coords[:-1]  # Remove duplicate closing coordinate

def _compute_axis_aligned_size_2d(plane_geometry_pts: CoordList) -> Tuple[float, float]:
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


def _geometry_to_y_down_plane_coords(geometry_pts: CoordList) -> List[List[float]]:
    """Process geometry coordinates, handling 3D projection if needed."""
    if len(geometry_pts) > 0 and len(geometry_pts[0]) >= 3:
        points_3d = np.array(geometry_pts, dtype=np.float32)
        normalized_coords = _pca_project_3d_facade_to_2d(points_3d)
        max_y = np.max(normalized_coords[:, 1])
        geometry = [[pt[0], max_y - pt[1]] for pt in normalized_coords]
    else:
        geometry_coords = np.array(geometry_pts)
        max_y = geometry_coords[:, 1].max()
        geometry = [[point[0], max_y - point[1]] for point in geometry_coords]
    return geometry


def _pca_project_3d_facade_to_2d(points_3d: np.ndarray) -> np.ndarray:
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


def _apply_homography_to_points(points: CoordList, matrix: np.ndarray) -> CoordList:
    """Transform a list of points using the given transformation matrix."""
    points_array = np.array(points, dtype=np.float32)
    ones = np.ones((len(points_array), 1), dtype=np.float32)
    homogeneous_points = np.hstack([points_array, ones])
    
    transformed_homogeneous = matrix @ homogeneous_points.T
    transformed_points = (transformed_homogeneous[:2] / transformed_homogeneous[2]).T
    
    return [(float(x), float(y)) for x, y in transformed_points]

def _index_corners_tl_tr_br_bl(points: np.ndarray) -> np.ndarray:
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
