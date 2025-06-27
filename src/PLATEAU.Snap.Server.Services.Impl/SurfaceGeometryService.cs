using NetTopologySuite.Geometries;
using PLATEAU.Snap.Models;
using PLATEAU.Snap.Models.Common;
using PLATEAU.Snap.Models.Extensions.Numerics;
using PLATEAU.Snap.Models.Server;
using PLATEAU.Snap.Server.Geoid;
using PLATEAU.Snap.Server.Repositories;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PLATEAU.Snap.Server.Services;

internal class SurfaceGeometryService : ISurfaceGeometryService
{
    private readonly ISurfaceGeometryRepository repository;

    private readonly ICityBoundaryRepository cityBoundaryRepository;

    private readonly IImageRepository imageRepository;

    private readonly Grid grid;

    private readonly GeometryFactory geometryFactory;

    public SurfaceGeometryService(ISurfaceGeometryRepository repository, ICityBoundaryRepository cityBoundaryRepository, IImageRepository imageRepository, Grid grid, GeometryFactory geometryFactory)
    {
        this.repository = repository;
        this.cityBoundaryRepository = cityBoundaryRepository;
        this.imageRepository = imageRepository;
        this.grid = grid;
        this.geometryFactory = geometryFactory;
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
            var thumbnailBytes = CreateThumbnailAsBytes(stream, 150, 150);

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
        var pageList = await this.repository.GetFacesAsync(buildingId, sortType, pageNumber, pageSize);
        return pageList.CreatePageDataWithSelect(x => new FaceImageInfo(x.FaceId!.Value, x.Gmlid, x.Thumbnail, x.Timestamp, x.IsOrtho!.Value));
    }

    public async Task<PageData<ImageInfo>> GetFaceImagesAsync(int buildingId, int faceId, SortType sortType, int pageNumber, int pageSize)
    {
        var pageList = await this.repository.GetFaceImagesAsync(buildingId, faceId, sortType, pageNumber, pageSize);
        return pageList.CreatePageDataWithSelect(x => new ImageInfo(x.ImageId, x.Gmlid, x.Thumbnail, x.Timestamp, x.IsOrtho!.Value));
    }

    public async Task<Models.Client.TransformResponse> TransformAsync(Models.Client.TransformRequest payload)
    {
        // Mock実装
        var preSignedURL = await this.imageRepository.GeneratePreSignedURLAsync("42.png", 60);
        var polygon = geometryFactory.CreatePolygon(
        [
            new Coordinate(77, 725),
            new Coordinate(77, 1790),
            new Coordinate(1100, 1790),
            new Coordinate(1100, 725),
            new Coordinate(77, 725)
        ]);
        return new Models.Client.TransformResponse(Models.Client.StatusType.Success, preSignedURL, polygon);
    }

    public async Task<Models.Client.RoofExtractionResponse> RoofExtractionAsync(Models.Client.RoofExtractionRequest payload)
    {
        // Mock実装
        var preSignedURL = await this.imageRepository.GeneratePreSignedURLAsync("103251.png", 60);
        var polygon = geometryFactory.CreatePolygon(
        [
            new Coordinate(105,40),
            new Coordinate(93,101),
            new Coordinate(233,133),
            new Coordinate(244,82),
            new Coordinate(212,75),
            new Coordinate(205,103),
            new Coordinate(143,91),
            new Coordinate(152,50),
            new Coordinate(105,40)
        ]);
        return new Models.Client.RoofExtractionResponse(Models.Client.StatusType.Success, preSignedURL, polygon);
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
