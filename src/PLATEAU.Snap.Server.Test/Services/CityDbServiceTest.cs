using PLATEAU.Snap.Models.Client;
using PLATEAU.Snap.Models.Exceptions;
using PLATEAU.Snap.Models.Settings;
using PLATEAU.Snap.Server.Entities.Models;
using PLATEAU.Snap.Server.Geoid;
using PLATEAU.Snap.Server.Services;
using PLATEAU.Snap.Server.Test.Fakes.Repositories;
using PLATEAU.Snap.Server.Test.Fakes.Services;

namespace PLATEAU.Snap.Server.Test.Services;

public class CityDbServiceTest
{
    private static readonly NetTopologySuite.Geometries.GeometryFactory geometryFactory = new NetTopologySuite.Geometries.GeometryFactory();

    private const int SurfaceGeometryCount = 3;

    [Fact(DisplayName = "テクスチャ更新")]
    [Trait("Category", "Unit")]
    public async Task ApplyTexture()
    {
        var service = CreateService();

        const int buildingId = 1;
        const int faceId = 1;
        const string path = "s3://temp/transform.png";
        const string coordinates = "POLYGON((10 10, 10 20, 20 20, 20 10, 10 10))";

        var request = new ApplyTextureRequest
        {
            BuildingId = buildingId,
            FaceId = faceId,
            Path = path,
            Coordinates = coordinates,
        };

        await service.ApplyTextureAsync(request);
    }

    [Fact(DisplayName = "テクスチャ更新 指定されたIDが存在しない")]
    [Trait("Category", "Unit")]
    public async Task ApplyTextureIdNotExists()
    {
        var service = CreateService();

        const int buildingId = 1000;
        const int faceId = 1000;
        const string path = "s3://temp/transform.png";
        const string coordinates = "POLYGON((10 10, 10 20, 20 20, 20 10, 10 10))";

        var request = new ApplyTextureRequest
        {
            BuildingId = buildingId,
            FaceId = faceId,
            Path = path,
            Coordinates = coordinates,
        };

        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.ApplyTextureAsync(request);
        });
        Assert.Equal(typeof(NotFoundException), exception.GetType());
    }

    [Fact(DisplayName = "テクスチャ更新 TexImageがnull")]
    [Trait("Category", "Unit")]
    public async Task ApplyTextureTexImageNull()
    {
        var imageRepository = new FakeImageRepository();
        imageRepository.IsTexImageNull = true;
        var service = CreateService(imageRepository);

        const int buildingId = 1;
        const int faceId = 1;
        const string path = "s3://temp/transform.png";
        const string coordinates = "POLYGON((10 10, 10 20, 20 20, 20 10, 10 10))";

        var request = new ApplyTextureRequest
        {
            BuildingId = buildingId,
            FaceId = faceId,
            Path = path,
            Coordinates = coordinates,
        };

        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.ApplyTextureAsync(request);
        });
        Assert.Equal(typeof(InvalidOperationException), exception.GetType());
    }

    private static CityDbService CreateService()
    {
        var imageRepository = new FakeImageRepository();
        var imageProcessingService = new FakeImageProcessingService();
        var grid = new Grid(null!);
        var appSettings = new AppSettings();
        var databaseSettings = new DatabaseSettings();

        SeedFakeData(imageRepository);

        return new CityDbService(imageRepository, imageProcessingService, appSettings, databaseSettings);
    }

    private static CityDbService CreateService(FakeImageRepository imageRepository)
    {
        var imageProcessingService = new FakeImageProcessingService();
        var grid = new Grid(null!);
        var appSettings = new AppSettings();
        var databaseSettings = new DatabaseSettings();

        SeedFakeData(imageRepository);

        return new CityDbService(imageRepository, imageProcessingService, appSettings, databaseSettings);
    }

    private static void SeedFakeData(FakeImageRepository repository)
    {
        var wktWriter = new NetTopologySuite.IO.WKTWriter();
        for (var surfaceGeometryId = 1; surfaceGeometryId <= SurfaceGeometryCount; surfaceGeometryId++)
        {
            var textureparam = new Textureparam
            {
                SurfaceGeometryId = surfaceGeometryId,
                IsTextureParametrization = 1,
                TextureCoordinates = geometryFactory.CreatePolygon(
                [
                    new NetTopologySuite.Geometries.Coordinate(10, 10),
                    new NetTopologySuite.Geometries.Coordinate(10, 20),
                    new NetTopologySuite.Geometries.Coordinate(20, 20),
                    new NetTopologySuite.Geometries.Coordinate(20, 10),
                    new NetTopologySuite.Geometries.Coordinate(10, 10),
                ]),
                SurfaceData = new SurfaceDatum
                {
                    TexImage = new TexImage()
                    {
                    }
                },
            };
            repository.Textureparams.Add(textureparam);
        }
    }
}
