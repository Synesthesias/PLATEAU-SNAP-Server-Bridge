using PLATEAU.Snap.Models.Client;

namespace PLATEAU.Snap.Server.Services;

public interface ICityDbService
{
    Task<Stream> ExportAsync(int id);

    Task<PreviewTextureResponse> PreviewTextureRequest(PreviewTextureRequest payload);

    Task ApplyTextureAsync(ApplyTextureRequest payload);
}
