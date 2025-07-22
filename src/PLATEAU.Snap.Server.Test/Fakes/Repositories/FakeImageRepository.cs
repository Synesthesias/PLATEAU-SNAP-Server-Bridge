using PLATEAU.Snap.Server.Entities.Models;
using PLATEAU.Snap.Server.Repositories;

namespace PLATEAU.Snap.Server.Test.Fakes.Repositories;

internal class FakeImageRepository : IImageRepository
{
    public List<Textureparam> Textureparams { get; } = new ();

    public bool IsTexImageNull { get; set; }

    public Task<Image> CreateAsync(Image image, Stream stream)
    {
        throw new NotImplementedException();
    }

    public Task<byte[]> DownloadAsync(string path)
    {
        return Task.FromResult(new byte[] { 0x01, 0x02, 0x03 });
    }

    public Task<string> GeneratePreSignedURLAsync(string path, int expiryInMinutes)
    {
        return Task.FromResult($"https://example.com/{path.Replace("s3://", "")}?expiry={expiryInMinutes}");
    }

    public async Task<Textureparam?> GetTextureparamAsync(int surfaceGeometryId)
    {
        var textureparam = Textureparams.FirstOrDefault(tp => tp.SurfaceGeometryId == surfaceGeometryId);
        if (IsTexImageNull && textureparam != null)
        {
            textureparam.SurfaceData.TexImage = null;
        }
        return await Task.FromResult(textureparam);
    }

    public async Task UpdateTextureparamAsync(Textureparam textureparam)
    {
        await Task.CompletedTask;
    }
}
