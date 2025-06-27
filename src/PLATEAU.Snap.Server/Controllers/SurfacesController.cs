using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLATEAU.Snap.Models.Client;
using PLATEAU.Snap.Models.Common;
using PLATEAU.Snap.Server.Filters;
using PLATEAU.Snap.Server.Services;
using Swashbuckle.AspNetCore.Annotations;
using static PLATEAU.Snap.Server.Constants;

namespace PLATEAU.Snap.Server.Controllers;

[Route(ApiRoute)]
[ApiController]
[Authorize]
[TypeFilter(typeof(ApiExceptionFilter))]
public class SurfacesController : ControllerBase
{
    private readonly ILogger<SurfacesController> logger;

    private readonly ISurfaceGeometryService service;

    public SurfacesController(ILogger<SurfacesController> logger, ISurfaceGeometryService service)
    {
        this.logger = logger;
        this.service = service;
    }

    [HttpPost]
    [Route("visible-surfaces")]
    [SwaggerOperation(
        Summary = "現在の位置で撮影可能な面の情報を取得します。",
        OperationId = nameof(GetVisibleSurfacesAsync)
    )]
    [SwaggerResponse(StatusCodes.Status200OK, SwaggerResponseDescriptions.Ok, typeof(VisibleSurfacesResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, SwaggerResponseDescriptions.BadRequest)]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, SwaggerResponseDescriptions.Unauthorized)]
    [SwaggerResponse(StatusCodes.Status404NotFound, SwaggerResponseDescriptions.NotFound)]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, SwaggerResponseDescriptions.InternalServerError)]
    public async Task<ActionResult<VisibleSurfacesResponse>> GetVisibleSurfacesAsync(
        [FromBody, SwaggerParameter("取得する面を絞り込むためのパラメータ", Required = true)] VisibleSurfacesRequest payload)
    {
        logger.LogInformation($"{DateTime.Now}: {payload}");
        return Ok(await service.GetVisibleSurfacesAsync(payload.ToServerParam()));
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
        logger.LogInformation($"{DateTime.Now}: {payload.Metadata}");

        var result = await service.CreateBuildingImageAsync(payload.ToServerParam());
        if (result.Status == StatusType.Error)
        {
            logger.LogWarning(result.Exception, $"{DateTime.Now}: Failed to create building image");
        }

        return Ok(result);
    }

    [HttpGet]
    [Route("buildings")]
    [SwaggerOperation(
        Summary = "テクスチャを更新できる建築物モデル情報を取得します。",
        OperationId = nameof(GetBuildingsAsync)
    )]
    [SwaggerResponse(StatusCodes.Status200OK, SwaggerResponseDescriptions.Ok, typeof(PageData<BuildingImage>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, SwaggerResponseDescriptions.BadRequest)]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, SwaggerResponseDescriptions.Unauthorized)]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, SwaggerResponseDescriptions.InternalServerError)]
    public async Task<ActionResult<PageData<BuildingImage>>> GetBuildingsAsync(
        [FromQuery, SwaggerParameter("ソート順")] SortType sort_type = SortType.id_asc,
        [FromQuery, SwaggerParameter("ページ番号")] int page_number = 1,
        [FromQuery, SwaggerParameter("ページサイズ")] int page_size = 10)
    {
        logger.LogInformation($"{DateTime.Now}: {sort_type}, {page_number}, {page_size}");
        return Ok(await service.GetBuildingsAsync(sort_type, page_number, page_size));
    }

    [HttpGet]
    [Route("faces/{building_id}")]
    [SwaggerOperation(
        Summary = "テクスチャを更新できる建築物モデルの面情報を取得します。",
        OperationId = nameof(GetFacesAsync)
    )]
    [SwaggerResponse(StatusCodes.Status200OK, SwaggerResponseDescriptions.Ok, typeof(PageData<ImageInfo>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, SwaggerResponseDescriptions.BadRequest)]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, SwaggerResponseDescriptions.Unauthorized)]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, SwaggerResponseDescriptions.InternalServerError)]
    public async Task<ActionResult<PageData<ImageInfo>>> GetFacesAsync(
        [FromRoute, SwaggerParameter("建築物モデルID")] int building_id,
        [FromQuery, SwaggerParameter("ソート順")] SortType sort_type = SortType.id_asc,
        [FromQuery, SwaggerParameter("ページ番号")] int page_number = 1,
        [FromQuery, SwaggerParameter("ページサイズ")] int page_size = 10)
    {
        logger.LogInformation($"{DateTime.Now}: {sort_type}, {page_number}, {page_size}");
        return Ok(await service.GetFacesAsync(building_id, sort_type, page_number, page_size));
    }

    [HttpGet]
    [Route("images/{building_id}/{face_id}")]
    [SwaggerOperation(
        Summary = "面に紐づけられた画像一覧を取得します。",
        OperationId = nameof(GetFaceImagesAsync)
    )]
    [SwaggerResponse(StatusCodes.Status200OK, SwaggerResponseDescriptions.Ok, typeof(PageData<ImageInfo>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, SwaggerResponseDescriptions.BadRequest)]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, SwaggerResponseDescriptions.Unauthorized)]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, SwaggerResponseDescriptions.InternalServerError)]
    public async Task<ActionResult<PageData<ImageInfo>>> GetFaceImagesAsync(
        [FromRoute, SwaggerParameter("建築物モデルID")] int building_id,
        [FromRoute, SwaggerParameter("面ID")] int face_id,
        [FromQuery, SwaggerParameter("ソート順")] SortType sort_type = SortType.id_asc,
        [FromQuery, SwaggerParameter("ページ番号")] int page_number = 1,
        [FromQuery, SwaggerParameter("ページサイズ")] int page_size = 10)
    {
        logger.LogInformation($"{DateTime.Now}: {sort_type}, {page_number}, {page_size}");
        return Ok(await service.GetFaceImagesAsync(building_id, face_id, sort_type, page_number, page_size));
    }

    [HttpPost]
    [Route("transform")]
    [SwaggerOperation(
        Summary = "指定された画像を正射変換します。",
        OperationId = nameof(TransformAsync)
    )]
    [SwaggerResponse(StatusCodes.Status200OK, SwaggerResponseDescriptions.Ok, typeof(TransformResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, SwaggerResponseDescriptions.BadRequest)]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, SwaggerResponseDescriptions.Unauthorized)]
    [SwaggerResponse(StatusCodes.Status404NotFound, SwaggerResponseDescriptions.NotFound)]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, SwaggerResponseDescriptions.InternalServerError)]
    public async Task<ActionResult<TransformResponse>> TransformAsync(
        [FromBody, SwaggerParameter("正射変換するためのパラメータ", Required = true)] TransformRequest payload)
    {
        logger.LogInformation($"{DateTime.Now}: {payload}");
        return Ok(await service.TransformAsync(payload));
    }

    [HttpPost]
    [Route("roof-extraction")]
    [SwaggerOperation(
        Summary = "PLATEAU-Ortho から屋根面画像を生成します。",
        OperationId = nameof(RoofExtractionAsync)
    )]
    [SwaggerResponse(StatusCodes.Status200OK, SwaggerResponseDescriptions.Ok, typeof(RoofExtractionResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, SwaggerResponseDescriptions.BadRequest)]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, SwaggerResponseDescriptions.Unauthorized)]
    [SwaggerResponse(StatusCodes.Status404NotFound, SwaggerResponseDescriptions.NotFound)]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, SwaggerResponseDescriptions.InternalServerError)]
    public async Task<ActionResult<RoofExtractionResponse>> RoofExtractionAsync(
        [FromBody, SwaggerParameter("正射変換するためのパラメータ", Required = true)] RoofExtractionRequest payload)
    {
        logger.LogInformation($"{DateTime.Now}: {payload}");
        return Ok(await service.RoofExtractionAsync(payload));
    }
}
