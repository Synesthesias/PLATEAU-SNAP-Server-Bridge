using PLATEAU.Snap.Models.Common;
using PLATEAU.Snap.Models.Server;

namespace PLATEAU.Snap.Server.Services;

public interface ISurfaceGeometryService
{
    Task<Models.Client.VisibleSurfacesResponse> GetVisibleSurfacesAsync(VisibleSurfacesRequest request);

    Task<Models.Client.BuildingImageResponse> CreateBuildingImageAsync(BuildingImageRequest request);

    Task<PageData<BuildingImage>> GetBuildingsAsync(SortType sortType, int pageNumber, int pageSize);

    Task<PageData<FaceImageInfo>> GetFacesAsync(int buildingId, SortType sortType, int pageNumber, int pageSize);

    Task<PageData<ImageInfo>> GetFaceImagesAsync(int buildingId, int faceId, SortType sortType, int pageNumber, int pageSize);

    Task<Models.Client.TransformResponse> TransformAsync(Models.Client.TransformRequest payload);

    Task<Models.Client.RoofExtractionResponse> RoofExtractionAsync(Models.Client.RoofExtractionRequest payload);
}
