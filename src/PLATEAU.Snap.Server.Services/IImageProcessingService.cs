using PLATEAU.Snap.Models.Lambda;

namespace PLATEAU.Snap.Server.Services;

public interface IImageProcessingService
{
    Task<LambdaTransformResponse> TransformAsync(LambdaTransformRequest request);

    Task<LambdaRoofExtractionResponse> RoofExtractionAsync(LambdaRoofExtractionRequest request);

    Task<LambdaApplyTextureResponse> ApplyTextureAsync(LambdaApplyTextureRequest request);
}
