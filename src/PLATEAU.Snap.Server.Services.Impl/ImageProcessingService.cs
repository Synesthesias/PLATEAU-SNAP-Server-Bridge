using Amazon.Lambda;
using Amazon.Lambda.Model;
using NetTopologySuite.Geometries;
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

    private readonly GeometryFactory geometryFactory;

    public ImageProcessingService(IAmazonLambda lambda, LambdaSettings lambdaSettings, GeometryFactory geometryFactory)
    {
        this.lambda = lambda;
        this.lambdaSettings = lambdaSettings;
        this.geometryFactory = geometryFactory;
    }

    public async Task<LambdaTransformResponse> TransformAsync(LambdaTransformRequest request)
    {
        // Mock実装
        var polygon = geometryFactory.CreatePolygon(
        [
            new Coordinate(77, 725),
            new Coordinate(77, 1790),
            new Coordinate(1100, 1790),
            new Coordinate(1100, 725),
            new Coordinate(77, 725)
        ]);

        var writer = new WKTWriter();
        var coordinates = writer.Write(polygon);

        var response = await Task.FromResult(new LambdaTransformResponse
        {
            Path = "s3://plateausnap-dev/42.png",
            Coordinates = coordinates,
        });

        //var response = await InvokeLambdaAsync<LambdaTransformRequest, LambdaTransformResponse>(this.lambdaSettings.TransformFunctionName, request);

        ValidateCoordinates(response.Coordinates);

        return response;
    }

    public async Task<LambdaRoofExtractionResponse> RoofExtractionAsync(LambdaRoofExtractionRequest request)
    {
        // Mock実装
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

        var writer = new WKTWriter();
        var coordinates = writer.Write(polygon);

        var response = await Task.FromResult(new LambdaRoofExtractionResponse
        {
            Path = "s3://plateausnap-dev/103251.png",
            Coordinates = coordinates,
        });

        //var response = await InvokeLambdaAsync<LambdaRoofExtractionRequest, LambdaRoofExtractionResponse>(this.lambdaSettings.RoofExtractionFunctionName, request);

        ValidateCoordinates(response.Coordinates);

        return response;
    }

    public async Task<LambdaApplyTextureResponse> ApplyTextureAsync(LambdaApplyTextureRequest request)
    {
        // Mock実装
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

        var writer = new WKTWriter();
        var coordinates = writer.Write(polygon);

        var response = await Task.FromResult(new LambdaApplyTextureResponse
        {
            Path = "s3://plateausnap-dev/ABC.png",
            TextureCoordinates = coordinates,
        });

        //var response = await InvokeLambdaAsync<LambdaApplyTextureRequest, LambdaApplyTextureResponse>(this.lambdaSettings.ApplyTextureFunctionName, request);

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
