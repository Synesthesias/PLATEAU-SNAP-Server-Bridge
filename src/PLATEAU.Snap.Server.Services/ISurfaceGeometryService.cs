using PLATEAU.Snap.Models.Common;
using PLATEAU.Snap.Models.Server;

namespace PLATEAU.Snap.Server.Services;

public interface ISurfaceGeometryService
{
    Task<Models.Client.VisibleSurfacesResponse> GetVisibleSurfacesAsync(VisibleSurfacesRequest request);

    Task<PageData<BuildingImage>> GetBuildingsAsync(SortType sortType, int pageNumber, int pageSize);

    Task<PageData<FaceImage>> GetFacesAsync(int buildingId, SortType sortType, int pageNumber, int pageSize);
}
