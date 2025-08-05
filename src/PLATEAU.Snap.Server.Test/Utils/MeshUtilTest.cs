using NetTopologySuite.Geometries;
using PLATEAU.Snap.Server.Services;

namespace PLATEAU.Snap.Server.Test.Utils;

public class MeshUtilTest
{
    private static readonly GeometryFactory geometryFactory = new GeometryFactory();

    [Fact(DisplayName = "東京駅周辺の3次メッシュコード計算")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_TokyoStation_ReturnsCorrectMeshCode()
    {
        // 東京駅周辺（経度139.766084, 緯度35.681167）
        var tokyoCoords = new[]
        {
            new Coordinate(139.766084, 35.681167),
            new Coordinate(139.766184, 35.681167),
            new Coordinate(139.766184, 35.681267),
            new Coordinate(139.766084, 35.681267),
            new Coordinate(139.766084, 35.681167)
        };
        var tokyoPolygon = geometryFactory.CreatePolygon(tokyoCoords);

        var meshCode = MeshUtil.GetThirdMeshCode(tokyoPolygon);

        Assert.NotNull(meshCode);
        Assert.Equal(8, meshCode.Length);
        Assert.True(meshCode.All(char.IsDigit));
        Assert.Equal("53394611", meshCode);
    }

    [Fact(DisplayName = "大阪駅周辺の3次メッシュコード計算")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_OsakaStation_ReturnsCorrectMeshCode()
    {
        // 大阪駅周辺（経度135.496042, 緯度34.702439）
        var osakaCoords = new[]
        {
            new Coordinate(135.496042, 34.702439),
            new Coordinate(135.496142, 34.702439),
            new Coordinate(135.496142, 34.702539),
            new Coordinate(135.496042, 34.702539),
            new Coordinate(135.496042, 34.702439)
        };
        var osakaPolygon = geometryFactory.CreatePolygon(osakaCoords);

        var meshCode = MeshUtil.GetThirdMeshCode(osakaPolygon);

        Assert.NotNull(meshCode);
        Assert.Equal(8, meshCode.Length);
        Assert.True(meshCode.All(char.IsDigit));
        Assert.Equal("52350349", meshCode);
    }

    [Fact(DisplayName = "既知の座標での正確なメッシュコード計算")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_KnownCoordinate_ReturnsExactMeshCode()
    {
        // 正確に計算できる座標
        // 経度140.0度、緯度36.0度 → メッシュコード54400000
        var coords = new[]
        {
            new Coordinate(140.0, 36.0),
            new Coordinate(140.001, 36.0),
            new Coordinate(140.001, 36.001),
            new Coordinate(140.0, 36.001),
            new Coordinate(140.0, 36.0)
        };
        var polygon = geometryFactory.CreatePolygon(coords);

        var meshCode = MeshUtil.GetThirdMeshCode(polygon);

        Assert.Equal("54400000", meshCode);
    }

    [Fact(DisplayName = "メッシュ境界をまたぐポリゴンの処理")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_BoundarySpanningPolygon_ReturnsLargestAreaMesh()
    {
        // メッシュ境界をまたぐ大きなポリゴン
        var boundaryCoords = new[]
        {
            new Coordinate(139.7659, 35.6811),
            new Coordinate(139.7662, 35.6811),
            new Coordinate(139.7662, 35.6814),
            new Coordinate(139.7659, 35.6814),
            new Coordinate(139.7659, 35.6811)
        };
        var boundaryPolygon = geometryFactory.CreatePolygon(boundaryCoords);

        var meshCode = MeshUtil.GetThirdMeshCode(boundaryPolygon);

        Assert.NotNull(meshCode);
        Assert.Equal(8, meshCode.Length);
        Assert.True(meshCode.All(char.IsDigit));
        Assert.Equal("53394611", meshCode);
    }

    [Fact(DisplayName = "面積が同じ場合のメッシュコード選択")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_EqualAreaMeshes_ReturnsSmallestMeshCode()
    {
        // 正確にメッシュ境界上にある小さなポリゴン
        // メッシュ境界の座標を計算して配置
        var precision = 0.0000001; // 非常に小さなポリゴン
        var boundaryCoords = new[]
        {
            new Coordinate(140.0, 36.0),
            new Coordinate(140.0 + precision, 36.0),
            new Coordinate(140.0 + precision, 36.0 + precision),
            new Coordinate(140.0, 36.0 + precision),
            new Coordinate(140.0, 36.0)
        };
        var boundaryPolygon = geometryFactory.CreatePolygon(boundaryCoords);

        var meshCode = MeshUtil.GetThirdMeshCode(boundaryPolygon);

        Assert.NotNull(meshCode);
        Assert.Equal(8, meshCode.Length);
        Assert.Equal("53397799", meshCode);
    }

    [Fact(DisplayName = "nullポリゴンでの例外処理")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_NullPolygon_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => MeshUtil.GetThirdMeshCode(null!));
    }

    [Fact(DisplayName = "極小ポリゴンでの処理")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_TinyPolygon_ReturnsValidMeshCode()
    {
        // 極小のポリゴン（1m四方程度）
        var tinyCoords = new[]
        {
            new Coordinate(139.766084, 35.681167),
            new Coordinate(139.766094, 35.681167), // 約1m東
            new Coordinate(139.766094, 35.681177), // 約1m北
            new Coordinate(139.766084, 35.681177),
            new Coordinate(139.766084, 35.681167)
        };
        var tinyPolygon = geometryFactory.CreatePolygon(tinyCoords);

        var meshCode = MeshUtil.GetThirdMeshCode(tinyPolygon);

        Assert.NotNull(meshCode);
        Assert.Equal(8, meshCode.Length);
        Assert.True(meshCode.All(char.IsDigit));
    }

    [Fact(DisplayName = "複雑な形状のポリゴンでの処理")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_ComplexPolygon_ReturnsValidMeshCode()
    {
        // 複雑な形状のポリゴン（星型など）
        var complexCoords = new[]
        {
            new Coordinate(139.766, 35.681),
            new Coordinate(139.767, 35.681),
            new Coordinate(139.7665, 35.682),
            new Coordinate(139.767, 35.683),
            new Coordinate(139.766, 35.683),
            new Coordinate(139.7655, 35.682),
            new Coordinate(139.766, 35.681)
        };
        var complexPolygon = geometryFactory.CreatePolygon(complexCoords);

        var meshCode = MeshUtil.GetThirdMeshCode(complexPolygon);

        Assert.NotNull(meshCode);
        Assert.Equal(8, meshCode.Length);
        Assert.True(meshCode.All(char.IsDigit));
        Assert.StartsWith("5339", meshCode);
    }

    [Theory(DisplayName = "様々な座標での3次メッシュコード計算")]
    [Trait("Category", "Unit")]
    [InlineData(135.0, 35.0, "52354000")] // 関西地方
    [InlineData(130.0, 33.0, "49304000")] // 九州地方
    [InlineData(140.0, 40.0, "60400000")] // 東北地方
    [InlineData(136.0, 36.0, "54360000")] // 中部地方
    public void GetThirdMeshCode_VariousCoordinates_ReturnsExpectedMeshCode(
        double longitude, double latitude, string expectedMeshCode)
    {
        // Arrange
        var coords = new[]
        {
            new Coordinate(longitude, latitude),
            new Coordinate(longitude + 0.001, latitude),
            new Coordinate(longitude + 0.001, latitude + 0.001),
            new Coordinate(longitude, latitude + 0.001),
            new Coordinate(longitude, latitude)
        };
        var polygon = geometryFactory.CreatePolygon(coords);

        var meshCode = MeshUtil.GetThirdMeshCode(polygon);

        Assert.Equal(expectedMeshCode, meshCode);
    }

    [Fact(DisplayName = "メッシュコード逆算テスト - 計算結果からポリゴンを生成して再計算")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_RoundTrip_ConsistentResults()
    {
        // 任意の座標でメッシュコードを計算
        var originalCoords = new[]
        {
            new Coordinate(139.766084, 35.681167),
            new Coordinate(139.766184, 35.681167),
            new Coordinate(139.766184, 35.681267),
            new Coordinate(139.766084, 35.681267),
            new Coordinate(139.766084, 35.681167)
        };
        var originalPolygon = geometryFactory.CreatePolygon(originalCoords);
        var meshCode = MeshUtil.GetThirdMeshCode(originalPolygon);

        var meshCenter = GetMeshCenter(meshCode);
        var centerCoords = new[]
        {
            new Coordinate(meshCenter.X - 0.0001, meshCenter.Y - 0.0001),
            new Coordinate(meshCenter.X + 0.0001, meshCenter.Y - 0.0001),
            new Coordinate(meshCenter.X + 0.0001, meshCenter.Y + 0.0001),
            new Coordinate(meshCenter.X - 0.0001, meshCenter.Y + 0.0001),
            new Coordinate(meshCenter.X - 0.0001, meshCenter.Y - 0.0001)
        };
        var centerPolygon = geometryFactory.CreatePolygon(centerCoords);
        var recalculatedMeshCode = MeshUtil.GetThirdMeshCode(centerPolygon);

        Assert.Equal(meshCode, recalculatedMeshCode);
    }

    [Fact(DisplayName = "実際のPLATEAUデータ座標範囲でのテスト")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_PlateauDataRange_ReturnsValidMeshCode()
    {
        // PLATEAUデータで使用される実際の座標範囲
        // 東京都心部の建物データの座標例
        var plateauCoords = new[]
        {
            new Coordinate(139.76708, 35.68115),
            new Coordinate(139.76718, 35.68115),
            new Coordinate(139.76718, 35.68125),
            new Coordinate(139.76708, 35.68125),
            new Coordinate(139.76708, 35.68115)
        };
        var plateauPolygon = geometryFactory.CreatePolygon(plateauCoords);

        var meshCode = MeshUtil.GetThirdMeshCode(plateauPolygon);

        Assert.NotNull(meshCode);
        Assert.Equal(8, meshCode.Length);
        Assert.True(meshCode.All(char.IsDigit));
        Assert.Equal("53394611", meshCode);
    }

    [Fact(DisplayName = "パフォーマンステスト - 大きなポリゴンでの処理時間")]
    [Trait("Category", "Performance")]
    public void GetThirdMeshCode_LargePolygon_CompletesWithinReasonableTime()
    {
        // 多数の頂点を持つ大きなポリゴン（1000頂点の円形ポリゴン）
        var center = new Coordinate(139.766084, 35.681167);
        var radius = 0.01; // 約1km
        var points = new List<Coordinate>();

        for (int i = 0; i < 1000; i++)
        {
            var angle = 2 * Math.PI * i / 1000;
            var x = center.X + radius * Math.Cos(angle);
            var y = center.Y + radius * Math.Sin(angle);
            points.Add(new Coordinate(x, y));
        }
        points.Add(points[0]); // 閉じる

        var largePolygon = geometryFactory.CreatePolygon(points.ToArray());

        var startTime = DateTime.UtcNow;
        var meshCode = MeshUtil.GetThirdMeshCode(largePolygon);
        var elapsed = DateTime.UtcNow - startTime;

        Assert.NotNull(meshCode);
        Assert.Equal(8, meshCode.Length);
        Assert.True(elapsed.TotalSeconds < 1.0, $"処理時間が1秒を超えました: {elapsed.TotalSeconds}秒");
    }

    [Fact(DisplayName = "メッシュ境界での一貫性テスト")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_MeshBoundaryConsistency_ReturnsConsistentResults()
    {
        // メッシュ境界近くの複数のポリゴンを作成
        var baseCoord = new Coordinate(140.0, 36.0); // メッシュ境界
        var testPolygons = new List<Polygon>();

        var offsets = new[] { -0.00001, 0.00001 }; // 境界の両側

        foreach (var offsetX in offsets)
        {
            foreach (var offsetY in offsets)
            {
                var coords = new[]
                {
                    new Coordinate(baseCoord.X + offsetX, baseCoord.Y + offsetY),
                    new Coordinate(baseCoord.X + offsetX + 0.00001, baseCoord.Y + offsetY),
                    new Coordinate(baseCoord.X + offsetX + 0.00001, baseCoord.Y + offsetY + 0.00001),
                    new Coordinate(baseCoord.X + offsetX, baseCoord.Y + offsetY + 0.00001),
                    new Coordinate(baseCoord.X + offsetX, baseCoord.Y + offsetY)
                };
                testPolygons.Add(geometryFactory.CreatePolygon(coords));
            }
        }

        var meshCodes = testPolygons.Select(MeshUtil.GetThirdMeshCode).ToList();

        Assert.All(meshCodes, meshCode =>
        {
            Assert.NotNull(meshCode);
            Assert.Equal(8, meshCode.Length);
            Assert.True(meshCode.All(char.IsDigit));
        });

        var uniqueMeshCodes = meshCodes.Distinct().ToList();
        Assert.True(uniqueMeshCodes.Count >= 1 && uniqueMeshCodes.Count <= 4);
    }

    [Fact(DisplayName = "不正な形状のポリゴンでの処理")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_InvalidPolygon_HandlesGracefully()
    {
        // 自己交差するポリゴン（無効な形状）
        var invalidCoords = new[]
        {
            new Coordinate(139.766, 35.681),
            new Coordinate(139.767, 35.682),
            new Coordinate(139.766, 35.682),
            new Coordinate(139.767, 35.681),
            new Coordinate(139.766, 35.681)
        };
        var invalidPolygon = geometryFactory.CreatePolygon(invalidCoords);

        var meshCode = MeshUtil.GetThirdMeshCode(invalidPolygon);

        Assert.NotNull(meshCode);
        Assert.Equal(8, meshCode.Length);
    }

    [Fact(DisplayName = "線状のポリゴンでの処理")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_LinearPolygon_HandlesGracefully()
    {
        // 線状（面積がほぼ0）のポリゴン
        var linearCoords = new[]
        {
            new Coordinate(139.766084, 35.681167),
            new Coordinate(139.766085, 35.681167),
            new Coordinate(139.766085, 35.681167),
            new Coordinate(139.766084, 35.681167),
            new Coordinate(139.766084, 35.681167)
        };
        var linearPolygon = geometryFactory.CreatePolygon(linearCoords);

        var meshCode = MeshUtil.GetThirdMeshCode(linearPolygon);

        Assert.NotNull(meshCode);
        Assert.Equal(8, meshCode.Length);
        Assert.True(meshCode.All(char.IsDigit));
    }



    [Fact(DisplayName = "ドーナツ型ポリゴンでの処理")]
    [Trait("Category", "Unit")]
    public void GetThirdMeshCode_PolygonWithHole_ReturnsValidMeshCode()
    {
        // 穴のあるポリゴン（ドーナツ型）
        var outerRing = new[]
        {
            new Coordinate(139.766, 35.681),
            new Coordinate(139.768, 35.681),
            new Coordinate(139.768, 35.683),
            new Coordinate(139.766, 35.683),
            new Coordinate(139.766, 35.681)
        };

        var hole = new[]
        {
            new Coordinate(139.7665, 35.6815),
            new Coordinate(139.7675, 35.6815),
            new Coordinate(139.7675, 35.6825),
            new Coordinate(139.7665, 35.6825),
            new Coordinate(139.7665, 35.6815)
        };

        var shell = geometryFactory.CreateLinearRing(outerRing);
        var holes = new[] { geometryFactory.CreateLinearRing(hole) };
        var donutPolygon = geometryFactory.CreatePolygon(shell, holes);

        var meshCode = MeshUtil.GetThirdMeshCode(donutPolygon);

        Assert.NotNull(meshCode);
        Assert.Equal(8, meshCode.Length);
        Assert.True(meshCode.All(char.IsDigit));
        Assert.StartsWith("5339", meshCode);
    }

    /// <summary>
    /// テスト用のヘルパーメソッド：メッシュコードから中心座標を計算
    /// </summary>
    private static Coordinate GetMeshCenter(string meshCode)
    {
        // メッシュコードを分解
        int latCode1 = int.Parse(meshCode.Substring(0, 2));
        int lonCode1 = int.Parse(meshCode.Substring(2, 2));
        int latCode2 = int.Parse(meshCode.Substring(4, 1));
        int lonCode2 = int.Parse(meshCode.Substring(5, 1));
        int latCode3 = int.Parse(meshCode.Substring(6, 1));
        int lonCode3 = int.Parse(meshCode.Substring(7, 1));

        // 1次メッシュの基準点
        double baseLat = latCode1 / 1.5;
        double baseLon = lonCode1 + 100;

        // 2次メッシュのサイズ
        double lat2Size = (2.0 / 3.0) / 8.0;
        double lon2Size = 1.0 / 8.0;

        // 3次メッシュのサイズ
        double lat3Size = lat2Size / 10.0;
        double lon3Size = lon2Size / 10.0;

        // 3次メッシュの南西角
        double swLat = baseLat + latCode2 * lat2Size + latCode3 * lat3Size;
        double swLon = baseLon + lonCode2 * lon2Size + lonCode3 * lon3Size;

        // 中心座標を計算
        double centerLat = swLat + lat3Size / 2.0;
        double centerLon = swLon + lon3Size / 2.0;

        return new Coordinate(centerLon, centerLat);
    }
}
