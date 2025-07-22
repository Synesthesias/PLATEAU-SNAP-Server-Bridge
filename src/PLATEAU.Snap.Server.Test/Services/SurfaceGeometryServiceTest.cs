using PLATEAU.Snap.Models.Client;
using PLATEAU.Snap.Models.Common;
using PLATEAU.Snap.Models.Exceptions;
using PLATEAU.Snap.Server.Entities.Models;
using PLATEAU.Snap.Server.Geoid;
using PLATEAU.Snap.Server.Services;
using PLATEAU.Snap.Server.Test.Fakes.Repositories;
using PLATEAU.Snap.Server.Test.Fakes.Services;

namespace PLATEAU.Snap.Server.Test.Services;

public class SurfaceGeometryServiceTest
{
    private static readonly NetTopologySuite.Geometries.GeometryFactory geometryFactory = new NetTopologySuite.Geometries.GeometryFactory();

    private const int BuildingCount = 18;

    private const int FaceCountPerBuilding = 5;

    private const int RoofCountPerBuilding = 2;

    private const int ImagePerFace = 4;

    [Fact(DisplayName = "建物一覧")]
    [Trait("Category", "Unit")]
    public async Task ListAiModels()
    {
        var service = CreateService();

        const int page = 1;
        const int pageSize = 10;
        const int totalCount = 18;
        const int totalPage = 2;
        const int count = 10;

        var pageData = await service.GetBuildingsAsync(SortType.id_asc, page, pageSize);
        Assert.Equal(totalCount, pageData.TotalCount);
        Assert.Equal(pageSize, pageData.PageSize);
        Assert.Equal(totalPage, pageData.TotalPages);
        Assert.Equal(page, pageData.CurrentPage);
        Assert.True(pageData.HasNext);
        Assert.Equal(count, pageData.Values.Count);
    }

    [Fact(DisplayName = "建物一覧 2ページ目")]
    [Trait("Category", "Unit")]
    public async Task ListAiModelsPage2()
    {
        var service = CreateService();

        const int page = 2;
        const int pageSize = 10;
        const int totalCount = 18;
        const int totalPage = 2;
        const int count = 8;

        var pageData = await service.GetBuildingsAsync(SortType.id_asc, page, pageSize);
        Assert.Equal(totalCount, pageData.TotalCount);
        Assert.Equal(pageSize, pageData.PageSize);
        Assert.Equal(totalPage, pageData.TotalPages);
        Assert.Equal(page, pageData.CurrentPage);
        Assert.False(pageData.HasNext);
        Assert.Equal(count, pageData.Values.Count);
    }

    [Fact(DisplayName = "建物一覧 ページサイズ")]
    [Trait("Category", "Unit")]
    public async Task ListAiModelsPageSize()
    {
        var service = CreateService();

        const int page = 2;
        const int pageSize = 5;
        const int totalCount = 18;
        const int totalPage = 4;
        const int count = 5;

        var pageData = await service.GetBuildingsAsync(SortType.id_asc, page, pageSize);
        Assert.Equal(totalCount, pageData.TotalCount);
        Assert.Equal(pageSize, pageData.PageSize);
        Assert.Equal(totalPage, pageData.TotalPages);
        Assert.Equal(page, pageData.CurrentPage);
        Assert.True(pageData.HasNext);
        Assert.Equal(count, pageData.Values.Count);
    }

    [Fact(DisplayName = "建物一覧 存在しないページ")]
    [Trait("Category", "Unit")]
    public async Task ListAiModelsOverPage()
    {
        var service = CreateService();

        const int page = 100;
        const int pageSize = 10;
        const int totalCount = 18;
        const int totalPage = 2;

        var pageData = await service.GetBuildingsAsync(SortType.id_asc, page, pageSize);
        Assert.Equal(totalCount, pageData.TotalCount);
        Assert.Equal(pageSize, pageData.PageSize);
        Assert.Equal(totalPage, pageData.TotalPages);
        Assert.Equal(page, pageData.CurrentPage);
        Assert.False(pageData.HasNext);
        Assert.Empty(pageData.Values);
    }

    [Fact(DisplayName = "面一覧")]
    [Trait("Category", "Unit")]
    public async Task GetFaces()
    {
        var service = CreateService();

        const int buildingId = 1;
        const int page = 1;
        const int pageSize = 10;
        const int totalCount = 7;
        const int totalPage = 1;
        const int count = 7;

        var pageData = await service.GetFacesAsync(buildingId, SortType.id_asc, page, pageSize);
        Assert.Equal(totalCount, pageData.TotalCount);
        Assert.Equal(pageSize, pageData.PageSize);
        Assert.Equal(totalPage, pageData.TotalPages);
        Assert.Equal(page, pageData.CurrentPage);
        Assert.False(pageData.HasNext);
        Assert.Equal(count, pageData.Values.Count);
    }

    [Fact(DisplayName = "面一覧 指定されたIDが存在しない")]
    [Trait("Category", "Unit")]
    public async Task GetFacesIdNotExists()
    {
        var service = CreateService();

        const int buildingId = 1000;
        const int page = 1;
        const int pageSize = 10;

        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.GetFacesAsync(buildingId, SortType.id_asc, page, pageSize);
        });
        Assert.Equal(typeof(NotFoundException), exception.GetType());
    }

    [Fact(DisplayName = "画像一覧")]
    [Trait("Category", "Unit")]
    public async Task GetFaceImages()
    {
        var service = CreateService();

        const int buildingId = 1;
        const int faceId = 1;
        const int page = 1;
        const int pageSize = 10;
        const int totalCount = 4;
        const int totalPage = 1;
        const int count = 4;

        var pageData = await service.GetFaceImagesAsync(buildingId, faceId, SortType.id_asc, page, pageSize);
        Assert.Equal(totalCount, pageData.TotalCount);
        Assert.Equal(pageSize, pageData.PageSize);
        Assert.Equal(totalPage, pageData.TotalPages);
        Assert.Equal(page, pageData.CurrentPage);
        Assert.False(pageData.HasNext);
        Assert.Equal(count, pageData.Values.Count);
    }

    [Fact(DisplayName = "画像一覧 指定されたIDが存在しない")]
    [Trait("Category", "Unit")]
    public async Task GetFaceImagesIdNotExists()
    {
        var service = CreateService();

        const int buildingId = 1000;
        const int faceId = 1000;
        const int page = 1;
        const int pageSize = 10;

        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.GetFaceImagesAsync(buildingId, faceId, SortType.id_asc, page, pageSize);
        });
        Assert.Equal(typeof(NotFoundException), exception.GetType());
    }

    [Fact(DisplayName = "正射変換")]
    [Trait("Category", "Unit")]
    public async Task Transform()
    {
        var service = CreateService();

        const int buildingId = 1;
        const int faceId = 1;
        const int imageId = 3;

        var request = new TransformRequest
        {
            BuildingId = buildingId,
            FaceId = faceId,
            ImageId = imageId,
        };

        var response = await service.TransformAsync(request);
        Assert.NotNull(response.Path);
        Assert.NotNull(response.Coordinates);
        Assert.NotNull(response.Uri);
    }

    [Fact(DisplayName = "正射変換 指定されたIDが存在しない")]
    [Trait("Category", "Unit")]
    public async Task TransformIdNotExists()
    {
        var service = CreateService();

        const int buildingId = 1000;
        const int faceId = 1000;
        const int imageId = 1000;

        var request = new TransformRequest
        {
            BuildingId = buildingId,
            FaceId = faceId,
            ImageId = imageId,
        };

        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.TransformAsync(request);
        });
        Assert.Equal(typeof(NotFoundException), exception.GetType());
    }

    [Fact(DisplayName = "正射変換 WKT取得失敗")]
    [Trait("Category", "Unit")]
    public async Task TransformWktFail()
    {
        var surfaceGeometryRepository = new FakeSurfaceGeometryRepository();
        surfaceGeometryRepository.IsFaceWktNull = true;
        var service = CreateService(surfaceGeometryRepository);

        const int buildingId = 1;
        const int faceId = 1;
        const int imageId = 3;

        var request = new TransformRequest
        {
            BuildingId = buildingId,
            FaceId = faceId,
            ImageId = imageId,
        };

        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.TransformAsync(request);
        });
        Assert.Equal(typeof(InvalidOperationException), exception.GetType());
    }

    [Fact(DisplayName = "屋根面生成")]
    [Trait("Category", "Unit")]
    public async Task RoofExtraction()
    {
        var service = CreateService();

        const int buildingId = 1;
        const int faceId = 6;

        var request = new RoofExtractionRequest
        {
            BuildingId = buildingId,
            FaceId = faceId,
        };

        var response = await service.RoofExtractionAsync(request);
        Assert.NotNull(response.Path);
        Assert.NotNull(response.Coordinates);
        Assert.NotNull(response.Uri);
    }

    [Fact(DisplayName = "屋根面生成 指定されたIDが存在しない")]
    [Trait("Category", "Unit")]
    public async Task RoofExtractionIdNotExists()
    {
        var service = CreateService();

        const int buildingId = 1000;
        const int faceId = 1000;

        var request = new RoofExtractionRequest
        {
            BuildingId = buildingId,
            FaceId = faceId,
        };

        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.RoofExtractionAsync(request);
        });
        Assert.Equal(typeof(NotFoundException), exception.GetType());
    }

    private static SurfaceGeometryService CreateService()
    {
        var surfaceGeometryRepository = new FakeSurfaceGeometryRepository();
        var cityBoundaryRepository = new FakeCityBoundaryRepository();
        var imageRepository = new FakeImageRepository();
        var imageProcessingService = new FakeImageProcessingService();
        var grid = new Grid(null!);

        SeedFakeData(surfaceGeometryRepository);

        return new SurfaceGeometryService(surfaceGeometryRepository, cityBoundaryRepository, imageRepository, imageProcessingService, grid);
    }

    private static SurfaceGeometryService CreateService(FakeSurfaceGeometryRepository surfaceGeometryRepository)
    {
        var cityBoundaryRepository = new FakeCityBoundaryRepository();
        var imageRepository = new FakeImageRepository();
        var imageProcessingService = new FakeImageProcessingService();
        var grid = new Grid(null!);

        SeedFakeData(surfaceGeometryRepository);

        return new SurfaceGeometryService(surfaceGeometryRepository, cityBoundaryRepository, imageRepository, imageProcessingService, grid);
    }

    private static void SeedFakeData(FakeSurfaceGeometryRepository repository)
    {
        var faceId = 1;
        var imageId = 1;
        for (var buildingId = 1; buildingId <= BuildingCount; buildingId++)
        {
            var endFaceId = faceId + FaceCountPerBuilding;
            for (; faceId < endFaceId; faceId++)
            {
                var endImageId = imageId + ImagePerFace;
                for (; imageId < endImageId; imageId++)
                {
                    var face = new BuildingFace
                    {
                        BuildingId = buildingId,
                        FaceId = faceId,
                        ImageId = imageId,
                        Gmlid = $"gmlid_{buildingId}_{faceId}",
                        IsOrtho = false,
                        Thumbnail = new byte[] { 0x01, 0x02, 0x03 },
                        Coordinates = geometryFactory.CreatePolygon(
                        [
                            new NetTopologySuite.Geometries.Coordinate(139.767052, 35.681167),
                            new NetTopologySuite.Geometries.Coordinate(139.767052, 35.681267),
                            new NetTopologySuite.Geometries.Coordinate(139.767152, 35.681267),
                            new NetTopologySuite.Geometries.Coordinate(139.767152, 35.681167),
                            new NetTopologySuite.Geometries.Coordinate(139.767052, 35.681167),
                        ]),
                        Timestamp = DateTime.UtcNow,
                    };
                    repository.BuildingFaces.Add(face);
                }
            }

            var endRoofId = faceId + RoofCountPerBuilding;
            for (; faceId < endRoofId; faceId++)
            {
                var roof = new BuildingFace
                {
                    BuildingId = buildingId,
                    FaceId = faceId,
                    Gmlid = $"roof_gmlid_{buildingId}_{faceId}",
                    IsOrtho = true,
                    Thumbnail = new byte[] { 0x04, 0x05, 0x06 },
                    Coordinates = geometryFactory.CreatePolygon(
                    [
                        new NetTopologySuite.Geometries.Coordinate(139.767052, 35.681167),
                        new NetTopologySuite.Geometries.Coordinate(139.767052, 35.681267),
                        new NetTopologySuite.Geometries.Coordinate(139.767152, 35.681267),
                        new NetTopologySuite.Geometries.Coordinate(139.767152, 35.681167),
                        new NetTopologySuite.Geometries.Coordinate(139.767052, 35.681167),
                    ]),
                    Timestamp = DateTime.UtcNow,
                };
                repository.BuildingFaces.Add(roof);
            }
        }
    }
}
