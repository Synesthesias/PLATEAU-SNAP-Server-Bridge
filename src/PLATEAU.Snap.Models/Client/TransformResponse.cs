using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PLATEAU.Snap.Models.Client;

public class TransformResponse
{
    [Required]
    [SwaggerSchema("結果のステータス", ReadOnly = true, Nullable = false)]
    public StatusType Status { get; set; }

    [Required]
    [SwaggerSchema("画像をダウンロードするための Presigned URL", ReadOnly = true, Nullable = true)]
    public string? Uri { get; set; }

    [Required]
    [SwaggerSchema("座標情報 (WKT形式)", ReadOnly = true, Nullable = true)]
    public string? Coordinates { get; set; }

    [JsonIgnore]
    public NetTopologySuite.Geometries.Polygon? Polygon { get; set; }

    public TransformResponse()
    {
    }

    public TransformResponse(StatusType status, string? uri, Polygon? polygon)
    {
        Status = status;
        Uri = uri;
        Polygon = polygon;

        if (polygon != null)
        {
            var writer = new WKTWriter();
            Coordinates = writer.Write(polygon);
        }
    }
}
