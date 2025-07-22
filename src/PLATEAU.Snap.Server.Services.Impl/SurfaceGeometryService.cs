using NetTopologySuite.IO;
using PLATEAU.Snap.Models.Common;
using PLATEAU.Snap.Models.Exceptions;
using PLATEAU.Snap.Models.Extensions.Numerics;
using PLATEAU.Snap.Models.Server;
using PLATEAU.Snap.Server.Geoid;
using PLATEAU.Snap.Server.Repositories;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PLATEAU.Snap.Server.Services;

internal class SurfaceGeometryService : ISurfaceGeometryService
{
    private const int ExpiryInMinutes = 60;

    private const int ThumbnailWidth = 300;

    private const int ThumbnailHeight = 300;

    private readonly ISurfaceGeometryRepository repository;

    private readonly ICityBoundaryRepository cityBoundaryRepository;

    private readonly IImageRepository imageRepository;

    private readonly IImageProcessingService imageProcessingService;

    private readonly Grid grid;

    public SurfaceGeometryService(ISurfaceGeometryRepository repository, ICityBoundaryRepository cityBoundaryRepository, IImageRepository imageRepository, IImageProcessingService imageProcessingService, Grid grid)
    {
        this.repository = repository;
        this.cityBoundaryRepository = cityBoundaryRepository;
        this.imageRepository = imageRepository;
        this.imageProcessingService = imageProcessingService;
        this.grid = grid;
    }

    public async Task<Models.Client.VisibleSurfacesResponse> GetVisibleSurfacesAsync(VisibleSurfacesRequest request)
    {
        // sridを取得
        var srid = await this.cityBoundaryRepository.GetSrid(request.From);

        // 候補の面を取得
        var polygons = await this.repository.GetPolygonInfoAsync(request, srid);
        if (polygons.Count == 0)
        {
            return new Models.Client.VisibleSurfacesResponse();
        }

        // カメラの情報を取得
        var cameraInfo = await this.repository.GetCameraInfoAsync(request, srid);

        // カメラの視野角内に入ってカメラの方を向いている面を取得
        var facingPolygons = GetFacingPolygons(polygons, cameraInfo);
        if (facingPolygons.Count == 0)
        {
            return new Models.Client.VisibleSurfacesResponse();
        }

        // 撮影地点のジオイド高を取得
        var geoidHeight = this.grid.GetGeoidHeight(request.From.X, request.From.Y);

        var response = new Models.Client.VisibleSurfacesResponse();
        response.Surfaces.AddRange(facingPolygons.Select(p => new Models.Client.Surface(p.Gmlid, p.Polygon, geoidHeight)));

        return response;
    }

    public async Task<Models.Client.BuildingImageResponse> CreateBuildingImageAsync(BuildingImageRequest request)
    {
        try
        {
            using var stream = request.File.OpenReadStream();
            var thumbnailBytes = CreateThumbnailAsBytes(stream, ThumbnailWidth, ThumbnailHeight);

            var entity = await this.imageRepository.CreateAsync(new Entities.Models.Image(request.Metadata, thumbnailBytes), stream);
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

    public async Task<PageData<BuildingImage>> GetBuildingsAsync(SortType sortType, int pageNumber, int pageSize)
    {
        var pageList = await this.repository.GetBuildingsAsync(sortType, pageNumber, pageSize);
        return pageList.CreatePageData();
    }

    public async Task<PageData<FaceImageInfo>> GetFacesAsync(int buildingId, SortType sortType, int pageNumber, int pageSize)
    {
        if (!(await this.repository.ExistsAsync(buildingId)))
        {
            throw new NotFoundException();
        }

        var pageList = await this.repository.GetFacesAsync(buildingId, sortType, pageNumber, pageSize);
        return pageList.CreatePageDataWithSelect(x => new FaceImageInfo(x.FaceId!.Value, x.Gmlid, x.Thumbnail, x.Timestamp, x.IsOrtho!.Value));
    }

    public async Task<PageData<ImageInfo>> GetFaceImagesAsync(int buildingId, int faceId, SortType sortType, int pageNumber, int pageSize)
    {
        if (!(await this.repository.ExistsAsync(buildingId, faceId)))
        {
            throw new NotFoundException();
        }

        var pageList = await this.repository.GetFaceImagesAsync(buildingId, faceId, sortType, pageNumber, pageSize);
        return pageList.CreatePageDataWithSelect(x => new ImageInfo(x.ImageId, x.Gmlid, x.Thumbnail, x.Timestamp, x.IsOrtho!.Value));
    }

    public async Task<Models.Client.TransformResponse> TransformAsync(Models.Client.TransformRequest payload)
    {
        var surfaceImage = await this.repository.GetSurfaceImageAsync(payload.BuildingId, payload.FaceId, payload.ImageId);
        if (surfaceImage is null)
        {
            throw new NotFoundException();
        }

        // アスペクト比の計算にジオイド高は考慮する必要がないため、ここでは無視する
        var wkt = await this.repository.GetFaceWktAsync(payload.FaceId);
        if (wkt is null)
        {
            throw new InvalidOperationException("The face geometry is not available.");
        }

        var writer = new WKTWriter();
        var coordinates = writer.Write(surfaceImage.Coordinates);

        var response = await imageProcessingService.TransformAsync(new Models.Lambda.LambdaTransformRequest()
        {
            Path = surfaceImage.Uri!,
            Coordinates = coordinates,
            Geometry = wkt
        });

        var preSignedURL = await this.imageRepository.GeneratePreSignedURLAsync(response.Path, ExpiryInMinutes);

        return new Models.Client.TransformResponse(response.Path, preSignedURL, response.Coordinates);
    }

    public async Task<Models.Client.RoofExtractionResponse> RoofExtractionAsync(Models.Client.RoofExtractionRequest payload)
    {
        var roofSurface = await this.repository.GetRoofSurfaceAsync(payload.BuildingId, payload.FaceId);
        if (roofSurface is null)
        {
            throw new NotFoundException();
        }

        var writer = new WKTWriter();
        var geometry = writer.Write(roofSurface.Geom);

        var response = await imageProcessingService.RoofExtractionAsync(new Models.Lambda.LambdaRoofExtractionRequest()
        {
            Geometry = geometry
        });

        var preSignedURL = await this.imageRepository.GeneratePreSignedURLAsync(response.Path, ExpiryInMinutes);

        return new Models.Client.RoofExtractionResponse(response.Path, preSignedURL, response.Coordinates);
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

    private List<PolygonInfo> GetFacingPolygons(List<PolygonInfo> polygons, CameraInfo cameraInfo)
    {
        // 候補の地物を充分に絞り込んでいるため、Listでよいと思われる
        // あまり数が多いならLinkedListの使用を検討する
        var list = new List<PolygonInfo>();
        foreach (var polygonInfo in polygons)
        {
            // 各面の法線ベクトルとカメラの方向ベクトルとのなす角を計算し、なす角が120度から180度程度の範囲に入る面を絞り込む
            var degrees = cameraInfo.Direction.Degrees(polygonInfo.Plane.Normal);
            if (degrees >= 120 && degrees <= 180)
            {
                list.Add(polygonInfo);
            }
        }

        return list;
    }
}
