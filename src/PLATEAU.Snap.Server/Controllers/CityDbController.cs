using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLATEAU.Snap.Models.Client;
using PLATEAU.Snap.Server.Filters;
using PLATEAU.Snap.Server.Services;
using Swashbuckle.AspNetCore.Annotations;
using static PLATEAU.Snap.Server.Constants;

namespace PLATEAU.Snap.Server.Controllers;

[Route(ApiRoute)]
[ApiController]
[Authorize]
[TypeFilter(typeof(ApiExceptionFilter))]
public class CityDbController : ControllerBase
{
    private readonly ILogger<CityDbController> logger;

    private readonly ICityDbService service;

    public CityDbController(ILogger<CityDbController> logger, ICityDbService service)
    {
        this.logger = logger;
        this.service = service;
    }

    [HttpPost]
    [Route("export")]
    [SwaggerOperation(
        Summary = "指定したIDの建物をCityGMLとしてエクスポートします。",
        OperationId = nameof(Export)
    )]
    [SwaggerResponse(StatusCodes.Status200OK, SwaggerResponseDescriptions.Ok, null, "application/zip")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, SwaggerResponseDescriptions.BadRequest)]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, SwaggerResponseDescriptions.Unauthorized)]
    [SwaggerResponse(StatusCodes.Status404NotFound, SwaggerResponseDescriptions.NotFound)]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, SwaggerResponseDescriptions.InternalServerError)]
    public async Task<ActionResult> Export(
        [FromBody, SwaggerParameter("エクスポートするためのパラメータ", Required = true)] ExportRequest payload)
    {
        logger.LogInformation($"{DateTime.Now}: {payload}");

        var stream = await service.ExportAsync(payload.Id);
        return File(stream, "application/zip", payload.FileName ?? $"{payload.Id}.zip");
    }
}
