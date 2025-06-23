namespace PLATEAU.Snap.Models.Common;

public class ImageInfo
{
    public long Id { get; set; }

    public string? Thumbnail { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public ImageInfo()
    {
    }

    public ImageInfo(long? id, byte[]? thumbnailBytes, DateTime? timestamp)
    {
        if (id is null)
        {
            throw new ArgumentNullException(nameof(id), "Image ID cannot be null.");
        }
        if (thumbnailBytes is null || thumbnailBytes.Length == 0)
        {
            throw new ArgumentNullException(nameof(thumbnailBytes), "Thumbnail bytes cannot be null or empty.");
        }
        if (timestamp is null)
        {
            throw new ArgumentNullException(nameof(timestamp), "Timestamp cannot be null.");
        }
        Id = id.Value;
        Thumbnail = Convert.ToBase64String(thumbnailBytes);
        Timestamp = timestamp.Value;
    }
}
