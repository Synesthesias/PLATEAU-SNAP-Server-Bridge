using PLATEAU.Snap.Models.Common;
using PLATEAU.Snap.Models.Server;

namespace PLATEAU.Snap.Server.Repositories;

public interface ISurfaceGeometryRepository
{
    Task<List<PolygonInfo>> GetPolygonInfoAsync(VisibleSurfacesRequest request, int srid);

    Task<CameraInfo> GetCameraInfoAsync(VisibleSurfacesRequest request, int srid);

    Task<PageList<BuildingImage>> GetBuildingsAsync(SortType sortType, int pageNumber, int pageSize);

    Task<PageList<FaceImage>> GetFacesAsync(int buildingId, SortType sortType, int pageNumber, int pageSize);
}
