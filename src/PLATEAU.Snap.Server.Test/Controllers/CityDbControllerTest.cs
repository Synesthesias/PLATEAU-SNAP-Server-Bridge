using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PLATEAU.Snap.Models.Client;
using PLATEAU.Snap.Server.Controllers;
using PLATEAU.Snap.Server.Test.Fixture;

namespace PLATEAU.Snap.Server.Test.Controllers;

public class CityDbControllerTest(TestFixture fixture) : IClassFixture<TestFixture>
{
    [Fact(DisplayName = "テクスチャ更新")]
    [Trait("Category", "Unit")]
    public async Task Transform()
    {
        var controller = fixture.GetRequiredService<CityDbController>();

        const int buildingId = 1;
        const int faceId = 1;
        const string path = "s3://temp/transform.png";
        const string coordinates = "POLYGON((10 10, 10 20, 20 20, 20 10, 10 10))";
        var request = new ApplyTextureRequest()
        {
            BuildingId = buildingId,
            FaceId = faceId,
            Path = path,
            Coordinates = coordinates
        };

        var actionResult = await controller.ApplyTextureAsync(request);

        var statusCodeResult = actionResult as StatusCodeResult;
        Assert.NotNull(statusCodeResult);
        Assert.Equal(StatusCodes.Status200OK, statusCodeResult.StatusCode);
    }

    [Fact(DisplayName = "CityGML出力")]
    [Trait("Category", "Unit")]
    public async Task Export()
    {
        var controller = fixture.GetRequiredService<CityDbController>();

        const int id = 1;
        const string fileName = "citygml.zip";
        var request = new ExportRequest()
        {
            Id = id,
            FileName = fileName
        };

        var actionResult = await controller.Export(request);

        var fileStreamResult = actionResult as FileStreamResult;
        Assert.NotNull(fileStreamResult);
        Assert.Equal("application/zip", fileStreamResult.ContentType);
        Assert.Equal(fileName, fileStreamResult.FileDownloadName);
    }
}
