using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using PLATEAU.Snap.Models.Client;
using PLATEAU.Snap.Models.Common;
using PLATEAU.Snap.Server.Controllers;
using PLATEAU.Snap.Server.Test.Fixture;

namespace PLATEAU.Snap.Server.Test.Controllers;

public class SurfacesControllerTest(TestFixture fixture) : IClassFixture<TestFixture>
{
    [Fact(DisplayName = "建物一覧")]
    [Trait("Category", "Unit")]
    public async Task GetBuildings()
    {
        var controller = fixture.GetRequiredService<SurfacesController>();

        const SortType sortType = SortType.id_asc;
        const int pageNumber = 1;
        const int pageSize = 10;

        var actionResult = await controller.GetBuildingsAsync(sortType, pageNumber, pageSize);

        var objectResult = actionResult.Result as IStatusCodeActionResult;
        Assert.NotNull(objectResult);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);
    }

    [Fact(DisplayName = "面一覧")]
    [Trait("Category", "Unit")]
    public async Task GetFaces()
    {
        var controller = fixture.GetRequiredService<SurfacesController>();

        const int buildingId = 1;
        const SortType sortType = SortType.id_asc;
        const int pageNumber = 1;
        const int pageSize = 10;

        var actionResult = await controller.GetFacesAsync(buildingId, sortType, pageNumber, pageSize);

        var objectResult = actionResult.Result as IStatusCodeActionResult;
        Assert.NotNull(objectResult);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);
    }

    [Fact(DisplayName = "画像一覧")]
    [Trait("Category", "Unit")]
    public async Task GetFaceImages()
    {
        var controller = fixture.GetRequiredService<SurfacesController>();

        const int buildingId = 1;
        const int faceId = 1;
        const SortType sortType = SortType.id_asc;
        const int pageNumber = 1;
        const int pageSize = 10;

        var actionResult = await controller.GetFaceImagesAsync(buildingId, faceId, sortType, pageNumber, pageSize);

        var objectResult = actionResult.Result as IStatusCodeActionResult;
        Assert.NotNull(objectResult);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);
    }

    [Fact(DisplayName = "正射変換")]
    [Trait("Category", "Unit")]
    public async Task Transform()
    {
        var controller = fixture.GetRequiredService<SurfacesController>();

        const int buildingId = 1;
        const int faceId = 1;
        const int imageId = 1;
        var request = new TransformRequest()
        {
            BuildingId = buildingId,
            FaceId = faceId,
            ImageId = imageId
        };

        var actionResult = await controller.TransformAsync(request);

        var objectResult = actionResult.Result as IStatusCodeActionResult;
        Assert.NotNull(objectResult);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);
    }

    [Fact(DisplayName = "屋根面生成")]
    [Trait("Category", "Unit")]
    public async Task RoofExtraction()
    {
        var controller = fixture.GetRequiredService<SurfacesController>();

        const int buildingId = 1;
        const int faceId = 1;
        var request = new RoofExtractionRequest
        {
            BuildingId = buildingId,
            FaceId = faceId,
        };

        var actionResult = await controller.RoofExtractionAsync(request);

        var objectResult = actionResult.Result as IStatusCodeActionResult;
        Assert.NotNull(objectResult);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);
    }
}
