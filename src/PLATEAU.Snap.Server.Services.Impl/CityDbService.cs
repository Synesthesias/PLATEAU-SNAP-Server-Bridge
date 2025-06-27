using PLATEAU.Snap.Models.Client;
using PLATEAU.Snap.Models.Settings;
using System.Diagnostics;

namespace PLATEAU.Snap.Server.Services;

internal class CityDbService : ICityDbService
{
    private readonly AppSettings appSettings;

    private readonly DatabaseSettings databaseSettings;

    public CityDbService(AppSettings appSettings, DatabaseSettings databaseSettings)
    {
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
        // Mock実装
        return await Task.FromResult(new PreviewTextureResponse
        {
        });
    }

    public async Task ApplyTextureAsync(ApplyTextureRequest payload)
    {
        // Mock実装
        await Task.CompletedTask;
    }
}
