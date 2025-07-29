using NetTopologySuite.Geometries;
using PLATEAU.Snap.Models.Client;
using PLATEAU.Snap.Models.Exceptions;
using PLATEAU.Snap.Models.Settings;
using PLATEAU.Snap.Server.Entities.Models;
using PLATEAU.Snap.Server.Repositories;
using System.Diagnostics;
using System.IO.Compression;

namespace PLATEAU.Snap.Server.Services;

internal class CityDbService : ICityDbService
{
    private readonly IImageRepository imageRepository;

    private readonly IImageProcessingService imageProcessingService;

    private readonly AppSettings appSettings;

    private readonly DatabaseSettings databaseSettings;

    public CityDbService(IImageRepository imageRepository, IImageProcessingService imageProcessingService, AppSettings appSettings, DatabaseSettings databaseSettings)
    {
        this.imageRepository = imageRepository;
        this.imageProcessingService = imageProcessingService;
        this.appSettings = appSettings;
        this.databaseSettings = databaseSettings;
    }

    public async Task<Stream> ExportAsync(int id)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            // 3D City Database Importer/Exporter に不具合があり、zip出力するとzipを閉じる際に例外がスローされることがある
            // これを回避するために、ツールではファイルとして出力し、自前でzip化している
            var outputDirectory = Path.Combine(tempDirectory, "citygml");
            var filePath = Path.Combine(outputDirectory, $"{id}.gml");
            Directory.CreateDirectory(outputDirectory!);
            var arguments = $"export -H {databaseSettings.Host} -P {databaseSettings.Port} -u {databaseSettings.Username} -p {databaseSettings.Password} -d {databaseSettings.Database} --db-id {id} -o {filePath}";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = appSettings.ImportExportToolPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Export failed with exit code {process.ExitCode}. Output: {output}, Error: {error}");
            }

            var zipFilePath = Path.ChangeExtension(tempDirectory, $"{id}.zip");
            ZipFile.CreateFromDirectory(outputDirectory, zipFilePath, CompressionLevel.Optimal, false);

            var memoryStream = new MemoryStream();
            using (var fileStream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read))
            {
                await fileStream.CopyToAsync(memoryStream);
            }
            memoryStream.Position = 0;

            return memoryStream;
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }

    public async Task<PreviewTextureResponse> PreviewTextureRequest(PreviewTextureRequest payload)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("PreviewTextureRequest is not implemented yet.");
    }

    public async Task ApplyTextureAsync(ApplyTextureRequest payload)
    {
        if (!(await this.imageRepository.FaceExists(payload.FaceId)))
        {
            throw new NotFoundException($"Face with ID {payload.FaceId} does not exist.");
        }

        var textureparam = await this.imageRepository.GetTextureparamAsync(payload.FaceId);

        var response = await imageProcessingService.ApplyTextureAsync(new Models.Lambda.LambdaApplyTextureRequest()
        {
            Path = payload.Path,
            Coordinates = payload.Coordinates,
        });

        var reader = new NetTopologySuite.IO.WKTReader();
        var textureCoordinates = reader.Read(response.TextureCoordinates) as Polygon ?? throw new InvalidOperationException($"{nameof(response.TextureCoordinates)} is not a valid polygon.");
        var fileBytes = await this.imageRepository.DownloadAsync(response.Path);
        string? mimeType = null;
        switch (Path.GetExtension(response.Path).ToLower())
        {
            case ".png":
                mimeType = "image/png";
                break;
            case ".jpg":
            case ".jpeg":
                mimeType = "image/jpg";
                break;
            case ".webp":
                mimeType = "image/webp";
                break;
            default:
                throw new InvalidOperationException($"Unsupported texture file type: {Path.GetExtension(response.Path)}");
        }

        if (textureparam is null)
        {
            // Textureparamが存在しない場合は新規追加
            await AddTextureparamAsync(payload.FaceId, textureCoordinates, mimeType, fileBytes);
        }
        else
        {
            if (textureparam.SurfaceData.TexImage is null)
            {
                throw new InvalidOperationException("SurfaceData is null in Textureparam.");
            }
            var count = await this.imageRepository.CountSurfaceData(textureparam.SurfaceData.TexImage.Id);

            if (count == 1)
            {
                // このTexImageに紐づくSurfaceDataが1つだけの場合は更新
                await UpdateTexImageAsync(textureparam, textureCoordinates, mimeType, fileBytes);
            }
            else if (count > 1)
            {
                // このTexImageに紐づくSurfaceDataが複数ある場合は新規追加
                await AddTexImageAsync(textureparam, textureCoordinates, mimeType, fileBytes);
            }
            else
            {
                throw new InvalidOperationException("No SurfaceData found for the given TexImageId.");
            }
        }
    }

    private async Task AddTextureparamAsync(int faceId, Polygon textureCoordinates, string mimeType, byte[] fileBytes)
    {
        var objectClass = await this.imageRepository.GetObjectClass("ParameterizedTexture");
        if (objectClass is null)
        {
            throw new InvalidOperationException("Object class 'ParameterizedTexture' not found.");
        }

        var textureparam = new Textureparam
        {
            SurfaceGeometryId = faceId,
            IsTextureParametrization = 1,
            TextureCoordinates = textureCoordinates,
            SurfaceData = new SurfaceDatum
            {
                Gmlid = $"ID_{Guid.NewGuid()}",
                IsFront = 1,
                ObjectclassId = objectClass.Id,
                TexImage = new TexImage
                {
                    TexMimeType = mimeType,
                    TexImageData = fileBytes
                }
            }
        };
        await this.imageRepository.AddTextureparamAsync(textureparam);
    }

    private async Task UpdateTexImageAsync(Textureparam textureparam, Polygon textureCoordinates, string mimeType, byte[] fileBytes)
    {
        if (textureparam.SurfaceData.TexImage is null)
        {
            throw new InvalidOperationException("TexImage is null in Textureparam.");
        }

        textureparam.TextureCoordinates = textureCoordinates;
        textureparam.SurfaceData.TexImage.TexMimeType = mimeType;
        textureparam.SurfaceData.TexImage.TexImageData = fileBytes;

        await this.imageRepository.UpdateTextureparamAsync(textureparam);
    }

    private async Task AddTexImageAsync(Textureparam textureparam, Polygon textureCoordinates, string mimeType, byte[] fileBytes)
    {
        var newTexImage = new TexImage
        {
            TexMimeType = mimeType,
            TexImageData = fileBytes
        };
        textureparam.SurfaceData.TexImage = newTexImage;
        textureparam.TextureCoordinates = textureCoordinates;
        await this.imageRepository.UpdateTextureparamAsync(textureparam);
    }
}
