using PLATEAU.Snap.Models.Client;
using PLATEAU.Snap.Server.Services;

namespace PLATEAU.Snap.Server.Test.Fakes.Services;

internal class FakeCityDbService : ICityDbService
{
    public Task<Stream> ExportAsync(int id)
    {
        return Task.FromResult<Stream>(new MemoryStream());
    }

    public Task<PreviewTextureResponse> PreviewTextureRequest(PreviewTextureRequest payload)
    {
        throw new NotImplementedException();
    }

    public Task ApplyTextureAsync(ApplyTextureRequest payload)
    {
        return Task.CompletedTask;
    }
}
