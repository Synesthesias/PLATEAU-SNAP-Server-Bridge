namespace PLATEAU.Snap.Models.Common
{
    public class BuildingImage
    {
        public int Id { get; set; }

        public string Gmlid { get; set; } = null!;

        public string? Thumbnail { get; set; } = null!;

        public string Address { get; set; } = null!;

        public BuildingImage()
        {
        }

        public BuildingImage(int id, string? gmlid, byte[]? thumbnailBytes, string? address)
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
            Address = !string.IsNullOrEmpty(address) ? address : "住所不明";
        }
    }
}
