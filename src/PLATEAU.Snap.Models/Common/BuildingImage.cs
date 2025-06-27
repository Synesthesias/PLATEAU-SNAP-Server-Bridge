using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace PLATEAU.Snap.Models.Common;

public class BuildingImage
{
    [Required]
    [SwaggerSchema("建物のID", ReadOnly = true, Nullable = false)]
    public int Id { get; set; }

    [Required]
    [SwaggerSchema("建物面のGML ID", ReadOnly = true, Nullable = false)]
    public string Gmlid { get; set; } = null!;

    [Required]
    [SwaggerSchema("サムネイル画像", ReadOnly = true, Nullable = false)]
    public string? Thumbnail { get; set; } = null!;

    [Required]
    [SwaggerSchema("住所", ReadOnly = true, Nullable = false)]
    public string Address { get; set; } = null!;

    public BuildingImage()
    {
    }

    public BuildingImage(int id, string? gmlid, byte[] thumbnailBytes, string address)
    {
        if (string.IsNullOrEmpty(gmlid))
        {
            throw new ArgumentNullException(nameof(gmlid));
        }

        Id = id;
        Gmlid = gmlid;
        Thumbnail = Convert.ToBase64String(thumbnailBytes);
        Address = address;
    }
}
