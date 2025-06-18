using PLATEAU.Snap.Models;
using PLATEAU.Snap.Models.Common;
using PLATEAU.Snap.Models.Server;
using PLATEAU.Snap.Server.Entities.Models;
using PLATEAU.Snap.Server.Repositories;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PLATEAU.Snap.Server.Services;

internal class ImageService : IImageService
{
    private readonly IImageRepository repository;

    private readonly ISurfaceGeometryRepository surfaceGeometryRepository;

    public ImageService(IImageRepository repository, ISurfaceGeometryRepository surfaceGeometryRepository)
    {
        this.repository = repository;
        this.surfaceGeometryRepository = surfaceGeometryRepository;
    }

    public async Task<Models.Client.BuildingImageResponse> CreateBuildingImageAsync(BuildingImageRequest request)
    {
        try
        {
            using var stream = request.File.OpenReadStream();
            var thumbnailBytes = CreateThumbnailAsBytes(stream, 150, 150);

            var entity = await this.repository.CreateAsync(new Image(request.Metadata, thumbnailBytes), stream);
            return new Models.Client.BuildingImageResponse()
            {
                Status = Models.Client.StatusType.Success,
                Id = entity.Id
            };
        }
        catch (SnapServerException ex)
        {
            return new Models.Client.BuildingImageResponse()
            {
                Status = Models.Client.StatusType.Error,
                Message = "File upload failed. Please try again.",
                Exception = ex
            };
        }
    }

    public async Task<PageData<BuildingImage>> GetBuildingImagesAsync(SortType sortType, int pageNumber, int pageSize)
    {
        var pageList = await this.surfaceGeometryRepository.GetBuildingImagesAsync(sortType, pageNumber, pageSize);
        return pageList.CreatePageData();
    }

    private static byte[] CreateThumbnailAsBytes(Stream inputStream, int width, int height)
    {
        inputStream.Position = 0;

        using var image = SixLabors.ImageSharp.Image.Load(inputStream);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new SixLabors.ImageSharp.Size(width, height)
        }));

        using var ms = new MemoryStream();
        image.Save(ms, new JpegEncoder());

        inputStream.Position = 0;

        return ms.ToArray();
    }
}
