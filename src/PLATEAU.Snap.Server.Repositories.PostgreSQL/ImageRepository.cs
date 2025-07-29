using Microsoft.EntityFrameworkCore;
using Npgsql;
using PLATEAU.Snap.Models.Exceptions;
using PLATEAU.Snap.Server.Entities;
using PLATEAU.Snap.Server.Entities.Models;
using System.Data;
using System.Net;

namespace PLATEAU.Snap.Server.Repositories;

internal class ImageRepository : BaseRepository, IImageRepository
{
    private readonly IStorageRepository storage;

    public ImageRepository(CitydbV4DbContext dbContext, IStorageRepository storage) : base(dbContext)
    {
        this.storage = storage;
    }

    public async Task<Image> CreateAsync(Image image, Stream stream)
    {
        try
        {
            using var connection = this.Context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT nextval('images_id_seq'::regclass)";
#pragma warning disable CS8605
            var id = (long)await command.ExecuteScalarAsync();
#pragma warning restore CS8605

            var response = await this.storage.UploadAsync(stream, $"{DateTime.Now.ToString("yyyy-MM-dd")}/{id}.png");
            if (response.StatusCode != HttpStatusCode.OK || response.Uri is null)
            {
                throw new SnapServerException($"Failed to upload image. [Upload] StatusCode: {response.StatusCode}");
            }

            image.Id = id;
            image.Uri = response.Uri;
            Context.Images.Add(image);
            await Context.SaveChangesAsync();

            return image;
        }
        catch (NpgsqlException ex)
        {
            throw new SnapServerException($"Failed to upload image. [Insert] ErrorCode: {ex.ErrorCode}, Message: {ex.Message}");
        }
    }

    public Task<string> GeneratePreSignedURLAsync(string path, int expiryInMinutes)
    {
        return this.storage.GeneratePreSignedURLAsync(path, expiryInMinutes);
    }

    public async Task<byte[]> DownloadAsync(string path)
    {
        return await this.storage.DownloadAsync(path);
    }

    public async Task<Textureparam?> GetTextureparamAsync(int surfaceGeometryId)
    {
        var textureparam = await Context.Textureparams
            .AsNoTracking()
            .Where(x => x.SurfaceGeometryId == surfaceGeometryId && x.IsTextureParametrization == 1)
            .Include(x => x.SurfaceData)
                .ThenInclude(x => x.TexImage)
            .FirstOrDefaultAsync(x => x.SurfaceGeometryId == surfaceGeometryId);

        return textureparam;
    }

    public async Task<bool> FaceExists(int surfaceGeometryId)
    {
        return await Context.SurfaceGeometries
            .AnyAsync(x => x.Id == surfaceGeometryId);
    }

    public async Task<int> CountSurfaceData(int texImageId)
    {
        return await Context.SurfaceData
            .Where(x => x.TexImageId == texImageId)
            .CountAsync();
    }

    public async Task UpdateTextureparamAsync(Textureparam textureparam)
    {
        Context.Textureparams.Update(textureparam);
        await Context.SaveChangesAsync();
    }

    public async Task AddTextureparamAsync(Textureparam textureparam)
    {
        Context.Textureparams.Add(textureparam);
        await Context.SaveChangesAsync();
    }

    public async Task<Objectclass?> GetObjectClass(string classname)
    {
        return await Context.Objectclasses
            .Where(x => x.Classname == classname)
            .FirstOrDefaultAsync();
    }
}
