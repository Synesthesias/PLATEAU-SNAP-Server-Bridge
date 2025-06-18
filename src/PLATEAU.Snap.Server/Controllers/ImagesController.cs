using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLATEAU.Snap.Models.Client;
using PLATEAU.Snap.Models.Common;
using PLATEAU.Snap.Server.Extensions.Mvc;
using PLATEAU.Snap.Server.Services;
using Swashbuckle.AspNetCore.Annotations;
using static PLATEAU.Snap.Server.Constants;

namespace PLATEAU.Snap.Server.Controllers;

[Route("api")]
[ApiController]
[Authorize]
public class ImagesController : ControllerBase
{
    private readonly ILogger<ImagesController> logger;

    private readonly IImageService service;

    public ImagesController(ILogger<ImagesController> logger, IImageService service)
    {
        this.logger = logger;
        this.service = service;
    }

    [HttpPost]
    [Route("building-image")]
    [SwaggerOperation(
        Summary = "撮影した建物面の画像を登録します。",
        OperationId = nameof(CreateBuildingImageAsync)
    )]
    [SwaggerResponse(StatusCodes.Status200OK, SwaggerResponseDescriptions.Ok, typeof(BuildingImageResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, SwaggerResponseDescriptions.BadRequest)]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, SwaggerResponseDescriptions.Unauthorized)]
    [SwaggerResponse(StatusCodes.Status404NotFound, SwaggerResponseDescriptions.NotFound)]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, SwaggerResponseDescriptions.InternalServerError)]
    public async Task<ActionResult<VisibleSurfacesResponse>> CreateBuildingImageAsync(
        [FromForm, SwaggerParameter("建物面の画像を登録するためのパラメータ", Required = true)] BuildingImageRequest payload)
    {
        try
        {
            logger.LogInformation($"{DateTime.Now}: {payload.Metadata}");

            var result = await service.CreateBuildingImageAsync(payload.ToServerParam());
            if (result.Status == StatusType.Error)
            {
                logger.LogWarning(result.Exception, $"{DateTime.Now}: Failed to create building image");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{DateTime.Now}: Failed to create building image");
            return this.HandleException(ex);
        }
    }

    [HttpGet]
    [Route("building-images")]
    [SwaggerOperation(
        Summary = "テクスチャを更新できる建築物モデル情報を取得します。",
        OperationId = nameof(GetBuildingImagesAsync)
    )]
    [SwaggerResponse(StatusCodes.Status200OK, SwaggerResponseDescriptions.Ok, typeof(PageData<BuildingImage>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, SwaggerResponseDescriptions.BadRequest)]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, SwaggerResponseDescriptions.Unauthorized)]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, SwaggerResponseDescriptions.InternalServerError)]
    public async Task<ActionResult<PageData<BuildingImage>>> GetBuildingImagesAsync(
        [FromQuery, SwaggerParameter("ソート順")] SortType sortType = SortType.IdAsc,
        [FromQuery, SwaggerParameter("ページ番号")] int pageNumber = 1,
        [FromQuery, SwaggerParameter("ページサイズ")] int pageSize = 10)
    {
        try
        {
            logger.LogInformation($"{DateTime.Now}: {sortType}, {pageNumber}, {pageSize}");
            return Ok(await service.GetBuildingImagesAsync(sortType, pageNumber, pageSize));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{DateTime.Now}: Failed to get visible surfaces");
            return this.HandleException(ex);
        }
    }
}
