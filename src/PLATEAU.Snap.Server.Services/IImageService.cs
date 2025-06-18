using PLATEAU.Snap.Models.Common;
using PLATEAU.Snap.Models.Server;

namespace PLATEAU.Snap.Server.Services;

public interface IImageService
{
    Task<Models.Client.BuildingImageResponse> CreateBuildingImageAsync(BuildingImageRequest request);

    Task<PageData<BuildingImage>> GetBuildingImagesAsync(SortType sortType, int pageNumber, int pageSize);
}
