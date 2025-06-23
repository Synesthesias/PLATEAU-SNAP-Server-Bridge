using PLATEAU.Snap.Models.Client;

namespace PLATEAU.Snap.Models.Server;

public class BuildingImageMetadata
{
    public string Gmlid { get; set; } = null!;

    public Coordinate From { get; set; } = null!;

    public Coordinate To { get; set; } = null!;

    public double Roll { get; set; }

    public List<float> Exterior { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Gmlid))
        {
            throw new ArgumentException($"{nameof(Gmlid)} is required.");
        }
        if (From == null)
        {
            throw new ArgumentException($"{nameof(From)} is required.");
        }
        if (To == null)
        {
            throw new ArgumentException($"{nameof(To)} is required.");
        }
        if (Exterior == null)
        {
            throw new ArgumentException($"{nameof(Exterior)} is required.");
        }
        // 最低3つの頂点分の座標が必要
        if (Exterior.Count < 6)
        {
            throw new ArgumentException($"{nameof(Exterior)} must contain at least 6 elements.");
        }
        // 外壁の頂点は偶数個でなければならない（2つの座標で1つの頂点を表すため）
        if (Exterior.Count % 2 != 0)
        {
            throw new ArgumentException($"{nameof(Exterior)} must contain an even number of elements.");
        }
        if (Timestamp == default)
        {
            throw new ArgumentException($"{nameof(Timestamp)} is required.");
        }

        From.Validate();
        To.Validate();
    }
}
