using PLATEAU.Snap.Server.Entities.Models;

namespace PLATEAU.Snap.Server.Repositories;

public interface IImageRepository
{
    Task<Image> CreateAsync(Image image, Stream stream);

    Task<string> GeneratePreSignedURLAsync(string path, int expiryInMinutes);

    Task<byte[]> DownloadAsync(string path);

    Task<Textureparam?> GetTextureparamAsync(int surfaceGeometryId);

    Task UpdateTextureparamAsync(Textureparam textureparam);
}
