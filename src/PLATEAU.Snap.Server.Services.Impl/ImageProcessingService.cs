using Amazon.Lambda;
using Amazon.Lambda.Model;
using NetTopologySuite.IO;
using PLATEAU.Snap.Models;
using PLATEAU.Snap.Models.Exceptions;
using PLATEAU.Snap.Models.Lambda;
using PLATEAU.Snap.Models.Settings;

namespace PLATEAU.Snap.Server.Services;

internal class ImageProcessingService : IImageProcessingService
{
    private readonly IAmazonLambda lambda;

    private readonly LambdaSettings lambdaSettings;

    public ImageProcessingService(IAmazonLambda lambda, LambdaSettings lambdaSettings)
    {
        this.lambda = lambda;
        this.lambdaSettings = lambdaSettings;
    }

    public async Task<LambdaTransformResponse> TransformAsync(LambdaTransformRequest request)
    {
        var response = await InvokeLambdaAsync<LambdaTransformRequest, LambdaTransformResponse>(this.lambdaSettings.TransformFunctionName, request);

        ValidateCoordinates(response.Coordinates);

        return response;
    }

    public async Task<LambdaRoofExtractionResponse> RoofExtractionAsync(LambdaRoofExtractionRequest request)
    {
        var response = await InvokeLambdaAsync<LambdaRoofExtractionRequest, LambdaRoofExtractionResponse>(this.lambdaSettings.RoofExtractionFunctionName, request);

        ValidateCoordinates(response.Coordinates);

        return response;
    }

    public async Task<LambdaApplyTextureResponse> ApplyTextureAsync(LambdaApplyTextureRequest request)
    {
        var response = await InvokeLambdaAsync<LambdaApplyTextureRequest, LambdaApplyTextureResponse>(this.lambdaSettings.ApplyTextureFunctionName, request);

        ValidateCoordinates(response.TextureCoordinates);

        return response;
    }

    private async Task<TResponse> InvokeLambdaAsync<TRequest, TResponse>(string functionName, TRequest request)
    {
        var invokeRequest = new InvokeRequest
        {
            FunctionName = functionName,
            Payload = Util.Serialize(request),
            InvocationType = InvocationType.RequestResponse
        };

        var invokeResponse = await this.lambda.InvokeAsync(invokeRequest);
        if (invokeResponse.HttpStatusCode != System.Net.HttpStatusCode.OK)
        {
            throw new LambdaOperationException($"Lambda function invocation failed with status code: {invokeResponse.HttpStatusCode}");
        }

        using var reader = new StreamReader(invokeResponse.Payload);
        var json = await reader.ReadToEndAsync();
        var response = Util.Deserialize<TResponse>(json);
        if (response is null)
        {
            throw new LambdaOperationException("Failed to deserialize Lambda response.");
        }

        return response;
    }

    private void ValidateCoordinates(string coordinates)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(coordinates))
            {
                throw new LambdaOperationException($"{nameof(coordinates)} cannot be null or empty.");
            }

            var reader = new WKTReader();
            var geometry = reader.Read(coordinates);
            if (geometry is null || !geometry.IsValid)
            {
                throw new LambdaOperationException($"{nameof(coordinates)} is not a valid geometry.");
            }
        }
        catch (Exception ex)
        {
            throw new LambdaOperationException($"Failed to parse {nameof(coordinates)}: {ex.Message}", ex);
        }
    }
}
