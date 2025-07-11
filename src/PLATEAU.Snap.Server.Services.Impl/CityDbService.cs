using NetTopologySuite.Geometries;
using PLATEAU.Snap.Models.Client;
using PLATEAU.Snap.Models.Exceptions;
using PLATEAU.Snap.Models.Settings;
using PLATEAU.Snap.Server.Repositories;
using System.Diagnostics;

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
            // ディレクトリはツールが自動的に作成するため、事前に作成する必要はない
            var path = Path.Combine(tempDirectory, Path.ChangeExtension(Path.GetRandomFileName(), ".zip"));
            var arguments = $"export -H {databaseSettings.Host} -P {databaseSettings.Port} -u {databaseSettings.Username} -p {databaseSettings.Password} -d {databaseSettings.Database} --db-id {id} -o {path}";

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

            var memoryStream = new MemoryStream();
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                await fileStream.CopyToAsync(memoryStream);
            }
            memoryStream.Position = 0;

            return memoryStream;
        }
        finally
        {
            if(Directory.Exists(tempDirectory))
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
        var textureparam = await this.imageRepository.GetTextureparamAsync(payload.FaceId);
        if (textureparam is null)
        {
            throw new NotFoundException($"Textureparam not found for FaceId: {payload.FaceId}");
        }
        if (textureparam.SurfaceData.TexImage is null)
        {
            throw new InvalidOperationException("TexImage is null in Textureparam.");
        }

        var response = await imageProcessingService.ApplyTextureAsync(new Models.Lambda.LambdaApplyTextureRequest()
        {
            Path = payload.Path,
            Coordinates = payload.Coordinates,
        });

        var reader = new NetTopologySuite.IO.WKTReader();
        textureparam.TextureCoordinates = reader.Read(response.TextureCoordinates) as Polygon;
        textureparam.SurfaceData.TexImage.TexImageData = await this.imageRepository.DownloadAsync(response.Path);

        await this.imageRepository.UpdateTextureparamAsync(textureparam);
    }
}
