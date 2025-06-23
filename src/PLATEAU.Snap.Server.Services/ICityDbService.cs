namespace PLATEAU.Snap.Server.Services;

public interface ICityDbService
{
    Task<Stream> ExportAsync(int id);
}
