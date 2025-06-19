namespace PLATEAU.Snap.Models.Common;

public class FaceImage
{
    public int Id { get; set; }

    public string Gmlid { get; set; } = null!;

    public string? Thumbnail { get; set; } = null!;

    public FaceImage()
    {
    }

    public FaceImage(int id, string? gmlid, byte[]? thumbnailBytes)
    {
        if (string.IsNullOrEmpty(gmlid))
        {
            throw new ArgumentNullException(nameof(gmlid));
        }

        Id = id;
        Gmlid = gmlid;
        if (thumbnailBytes is not null && thumbnailBytes.Length > 0)
        {
            Thumbnail = Convert.ToBase64String(thumbnailBytes);
        }
    }
}
